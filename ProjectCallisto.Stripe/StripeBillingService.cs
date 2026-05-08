using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectCallisto.Application.Billing;
using ProjectCallisto.EfCore;
using Stripe;
using Stripe.Checkout;
using BillingPortalSessionService = Stripe.BillingPortal.SessionService;
using BillingPortalSessionOptions = Stripe.BillingPortal.SessionCreateOptions;
using CheckoutSessionService = Stripe.Checkout.SessionService;

namespace ProjectCallisto.Stripe;

public class StripeBillingService : IBillingService
{
    private readonly IStripeClient _stripeClient;
    private readonly ILogger<StripeBillingService> _logger;
    private readonly AppDbContext _context;
    private readonly StripeOptions _stripeOptions;


    public StripeBillingService(
        IStripeClient stripeClient,
        ILogger<StripeBillingService> logger,
        AppDbContext context,
        IOptions<StripeOptions> stripeOptions)
    {
        _stripeClient = stripeClient;
        _logger = logger;
        _context = context;
        _stripeOptions = stripeOptions.Value;
    }
    
    public async Task<CheckoutResult> CreateCheckoutSessionAsync(
        Guid organisationId,
        int seatCount,
        BillingInterval billingInterval)
    {
        _logger.LogInformation(
            "Creating checkout session for organisation {OrganisationId}, {SeatCount} seats, {BillingInterval}",
            organisationId, seatCount, billingInterval);

        // Validate seat count
        if (seatCount < 5)
        {
            throw new ArgumentException("Minimum seat count is 5", nameof(seatCount));
        }

        // Get organisation
        var organisation = await _context.Organisations
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == organisationId);

        if (organisation == null)
        {
            throw new InvalidOperationException($"Organisation {organisationId} not found");
        }

        // Check if downgrading - prevent if assigned seats > requested seats
        var assignedSeats = await _context.TenantMembers
            .CountAsync(tm => tm.OrganisationId == organisationId && tm.IsAssignedSeat);

        if (seatCount < assignedSeats)
        {
            throw new ArgumentException(
                $"Cannot downgrade to {seatCount} seats. You currently have {assignedSeats} assigned seats. " +
                $"Please unassign {assignedSeats - seatCount} seat(s) before downgrading.",
                nameof(seatCount));
        }

        var subscription = organisation.Subscription;
        var priceService = new PriceService(_stripeClient);
        StripeList<Price> prices;

        // If they have an active subscription, verify with Stripe first before updating
        if (subscription?.Status == Domain.Organisations.SubscriptionStatus.Active
            && !string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            _logger.LogInformation(
                "Verifying subscription {SubscriptionId} status in Stripe for organisation {OrganisationId}",
                subscription.StripeSubscriptionId, organisationId);

            var subscriptionService = new SubscriptionService(_stripeClient);
            var stripeSubscription = await subscriptionService.GetAsync(subscription.StripeSubscriptionId);

            // Verify subscription is actually active in Stripe (source of truth)
            if (stripeSubscription == null || stripeSubscription.Status != "active")
            {
                _logger.LogWarning(
                    "Subscription {SubscriptionId} is not active in Stripe (status: {Status}), syncing local state",
                    subscription.StripeSubscriptionId, stripeSubscription?.Status ?? "not found");

                // Sync local state with Stripe
                subscription.Status = stripeSubscription?.Status switch
                {
                    "past_due" => Domain.Organisations.SubscriptionStatus.PastDue,
                    "canceled" => Domain.Organisations.SubscriptionStatus.Cancelled,
                    _ => Domain.Organisations.SubscriptionStatus.Cancelled
                };
                await _context.SaveChangesAsync();

                // Fall through to checkout flow - they need to create a new subscription
                _logger.LogInformation(
                    "Redirecting to checkout to create new subscription for organisation {OrganisationId}",
                    organisationId);
            }
            else
            {
                // Subscription is verified active in Stripe - proceed with update
                _logger.LogInformation(
                    "Subscription verified active, updating for organisation {OrganisationId}",
                    organisationId);

                // Get the current subscription item
                var subscriptionItem = stripeSubscription.Items.Data.FirstOrDefault();
                if (subscriptionItem == null)
                {
                    throw new InvalidOperationException(
                        $"No subscription items found for subscription {subscription.StripeSubscriptionId}");
                }

                var oldSeatCount = subscription.PaidSeats;

                // Check if they're trying to change billing interval
                var currentInterval = subscriptionItem.Price.Recurring?.Interval;
                var requestedInterval = billingInterval == BillingInterval.Monthly ? "month" : "year";

                if (currentInterval != requestedInterval)
                {
                    // Billing interval change - fetch new price and swap it
                    _logger.LogInformation(
                        "Billing interval change detected ({CurrentInterval} → {RequestedInterval}), updating price",
                        currentInterval, requestedInterval);

                    var newPriceLookupKey = billingInterval == BillingInterval.Monthly
                        ? "monthly_volume"
                        : "annual_volume";


                    prices = await priceService.ListAsync(new PriceListOptions
                    {
                        LookupKeys = new List<string> { newPriceLookupKey },
                        Limit = 1
                    });
                    var newPrice = prices.FirstOrDefault();
                    if (newPrice == null)
                    {
                        throw new InvalidOperationException(
                            $"Price with lookup key {newPriceLookupKey} not found in Stripe");
                    }

                    // Update subscription with new price AND quantity
                    await subscriptionService.UpdateAsync(subscription.StripeSubscriptionId, new SubscriptionUpdateOptions
                    {
                        Items = new List<SubscriptionItemOptions>
                        {
                            new SubscriptionItemOptions
                            {
                                Id = subscriptionItem.Id,
                                Price = newPrice.Id, // Swap to new billing interval price
                                Quantity = seatCount
                            }
                        },
                        ProrationBehavior = "always_invoice" // Charge/credit immediately
                    });

                    // Update local database
                    subscription.PaidSeats = seatCount;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Subscription updated successfully - interval changed to {NewInterval}, seats: {OldSeats} → {NewSeats}",
                        requestedInterval, oldSeatCount, seatCount);

                    var intervalChanged = billingInterval == BillingInterval.Monthly ? "monthly" : "annual";
                    return new CheckoutResult
                    {
                        Success = true,
                        Message = $"Subscription updated to {intervalChanged} billing with {seatCount} seats"
                    };
                }
                else
                {
                    // Same billing interval - just update quantity
                    await subscriptionService.UpdateAsync(subscription.StripeSubscriptionId, new SubscriptionUpdateOptions
                    {
                        Items = new List<SubscriptionItemOptions>
                        {
                            new SubscriptionItemOptions
                            {
                                Id = subscriptionItem.Id,
                                Quantity = seatCount
                            }
                        },
                        ProrationBehavior = "always_invoice" // Charge/credit immediately
                    });

                    // Update local database
                    subscription.PaidSeats = seatCount;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Subscription updated successfully - old: {OldSeats}, new: {NewSeats}",
                        oldSeatCount, seatCount);

                    return new CheckoutResult
                    {
                        Success = true,
                        Message = seatCount > oldSeatCount
                            ? $"Subscription upgraded to {seatCount} seats"
                            : $"Subscription downgraded to {seatCount} seats"
                    };
                }
            }
        }

        // Create checkout session for new subscriptions or billing interval changes
        // Determine price lookup key based on billing interval
        var priceLookupKey = billingInterval == BillingInterval.Monthly
            ? "monthly_volume"
            : "annual_volume";


        prices = await priceService.ListAsync(new PriceListOptions
        {
            LookupKeys = new List<string> { priceLookupKey },
            Limit = 1
        });
        var price = prices.FirstOrDefault();
        if (price == null)
        {
            throw new InvalidOperationException($"Price with lookup key {priceLookupKey} not found in Stripe");
        }
        
        // Create or get Stripe customer
        var customerId = await GetOrCreateCustomerAsync(organisation);

        // Create checkout session
        var sessionService = new CheckoutSessionService(_stripeClient);
        var successUrl = $"{_stripeOptions.CheckoutSuccessUrl}/organisation/{organisationId}/subscription?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{_stripeOptions.CheckoutCancelUrl}/organisation/{organisationId}/pricing";

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = price.Id,
                    Quantity = seatCount,
                }
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                { "organisation_id", organisationId.ToString() },
                { "seat_count", seatCount.ToString() },
                { "billing_interval", billingInterval.ToString() }
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "organisation_id", organisationId.ToString() }
                }
            }
        };

        var session = await sessionService.CreateAsync(options);

        _logger.LogInformation(
            "Checkout session created: {SessionId} for organisation {OrganisationId}",
            session.Id, organisationId);

        return new CheckoutResult
        {
            Success = true,
            SessionId = session.Id,
            SessionUrl = session.Url
        };
    }

    private async Task<string> GetOrCreateCustomerAsync(Domain.Organisations.Organisation organisation)
    {
        // If customer already exists in Stripe, return it
        if (!string.IsNullOrEmpty(organisation.StripeCustomerId))
        {
            return organisation.StripeCustomerId;
        }

        // Create new Stripe customer
        var customerService = new CustomerService(_stripeClient);
        var options = new CustomerCreateOptions
        {
            Name = organisation.Name,
            Metadata = new Dictionary<string, string>
            {
                { "organisation_id", organisation.Id.ToString() },
                { "tenant_id", organisation.TenantId }
            }
        };

        var customer = await customerService.CreateAsync(options);

        // Store customer ID in database
        organisation.StripeCustomerId = customer.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created Stripe customer {CustomerId} for organisation {OrganisationId}",
            customer.Id, organisation.Id);

        return customer.Id;
    }

    public async Task<SubscriptionDetails> GetSubscriptionAsync(Guid organisationId)
    {
        _logger.LogInformation("Fetching subscription details for organisation {OrganisationId}", organisationId);

        var organisation = await _context.Organisations
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == organisationId);

        if (organisation == null)
        {
            throw new InvalidOperationException($"Organisation {organisationId} not found");
        }

        var subscription = organisation.Subscription;
        if (subscription == null)
        {
            throw new InvalidOperationException($"Organisation {organisationId} has no subscription");
        }

        // If trial, return trial details
        if (subscription.Status == Domain.Organisations.SubscriptionStatus.Trial)
        {
            return new SubscriptionDetails
            {
                Status = "Trial",
                PaidSeats = subscription.PaidSeats,
                TrialEndsAt = subscription.TrialEndsAt?.UtcDateTime,
                CurrentPeriodEnd = null,
                BillingInterval = null,
                PricePerSeat = null,
                StripeSubscriptionId = null
            };
        }

        // If active with Stripe subscription, fetch from Stripe
        if (subscription.Status == Domain.Organisations.SubscriptionStatus.Active
            && !string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            var subscriptionService = new SubscriptionService(_stripeClient);
            var stripeSubscription = await subscriptionService.GetAsync(subscription.StripeSubscriptionId);

            if (stripeSubscription == null)
            {
                _logger.LogWarning(
                    "Stripe subscription {SubscriptionId} not found for organisation {OrganisationId}",
                    subscription.StripeSubscriptionId, organisationId);

                // Fall back to local data
                return new SubscriptionDetails
                {
                    Status = subscription.Status.ToString(),
                    PaidSeats = subscription.PaidSeats,
                    TrialEndsAt = null,
                    CurrentPeriodEnd = null,
                    BillingInterval = null,
                    PricePerSeat = null,
                    StripeSubscriptionId = subscription.StripeSubscriptionId
                };
            }

            // Extract billing interval from Stripe subscription
            var interval = stripeSubscription.Items.Data.FirstOrDefault()?.Price.Recurring?.Interval;
            BillingInterval? billingInterval = interval switch
            {
                "month" => Application.Billing.BillingInterval.Monthly,
                "year" => Application.Billing.BillingInterval.Annual,
                _ => null
            };

            // Extract price per seat from Stripe
            var pricePerSeatCents = stripeSubscription.Items.Data.FirstOrDefault()?.Price.UnitAmount;
            decimal? pricePerSeat = pricePerSeatCents.HasValue
                ? pricePerSeatCents.Value / 100m
                : null;

            // Get seat count from Stripe subscription
            var seatCount = (int)(stripeSubscription.Items.Data.FirstOrDefault()?.Quantity ?? subscription.PaidSeats);

            return new SubscriptionDetails
            {
                Status = "Active",
                PaidSeats = seatCount,
                CurrentPeriodEnd = stripeSubscription.Items.Data[0].CurrentPeriodEnd,
                BillingInterval = billingInterval,
                PricePerSeat = pricePerSeat,
                TrialEndsAt = null,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                CancelAtPeriodEnd = stripeSubscription.CancelAtPeriodEnd,
                CancelAt = stripeSubscription.CancelAt
            };
        }

        // For any other status (PastDue, Cancelled), return local data
        return new SubscriptionDetails
        {
            Status = subscription.Status.ToString(),
            PaidSeats = subscription.PaidSeats,
            TrialEndsAt = subscription.TrialEndsAt?.UtcDateTime,
            CurrentPeriodEnd = null,
            BillingInterval = null,
            PricePerSeat = null,
            StripeSubscriptionId = subscription.StripeSubscriptionId
        };
    }

    public async Task CancelSubscriptionAsync(Guid organisationId)
    {
        _logger.LogInformation("Cancelling subscription for organisation {OrganisationId}", organisationId);

        var organisation = await _context.Organisations
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == organisationId);

        if (organisation?.Subscription == null)
        {
            throw new InvalidOperationException($"Organisation {organisationId} has no subscription");
        }

        var subscription = organisation.Subscription;

        if (subscription.Status != Domain.Organisations.SubscriptionStatus.Active)
        {
            throw new InvalidOperationException("Only active subscriptions can be cancelled");
        }

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            throw new InvalidOperationException("Subscription has no Stripe subscription ID");
        }

        // Cancel at period end in Stripe
        var subscriptionService = new SubscriptionService(_stripeClient);
        await subscriptionService.UpdateAsync(subscription.StripeSubscriptionId, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true
        });

        _logger.LogInformation(
            "Subscription {SubscriptionId} set to cancel at period end for organisation {OrganisationId}",
            subscription.StripeSubscriptionId, organisationId);
    }

    public async Task UncancelSubscriptionAsync(Guid organisationId)
    {
        _logger.LogInformation("Uncancelling subscription for organisation {OrganisationId}", organisationId);

        var organisation = await _context.Organisations
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == organisationId);

        if (organisation?.Subscription == null)
        {
            throw new InvalidOperationException($"Organisation {organisationId} has no subscription");
        }

        var subscription = organisation.Subscription;

        if (subscription.Status != Domain.Organisations.SubscriptionStatus.Active)
        {
            throw new InvalidOperationException("Only active subscriptions can be uncancelled");
        }

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            throw new InvalidOperationException("Subscription has no Stripe subscription ID");
        }

        // Remove cancel_at_period_end in Stripe
        var subscriptionService = new SubscriptionService(_stripeClient);
        await subscriptionService.UpdateAsync(subscription.StripeSubscriptionId, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false
        });

        _logger.LogInformation(
            "Subscription {SubscriptionId} uncancelled for organisation {OrganisationId}",
            subscription.StripeSubscriptionId, organisationId);
    }

    public async Task<string> CreateCustomerPortalSessionAsync(Guid organisationId)
    {
        _logger.LogInformation("Creating customer portal session for organisation {OrganisationId}", organisationId);

        var organisation = await _context.Organisations
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == organisationId);

        if (organisation == null)
        {
            throw new InvalidOperationException($"Organisation {organisationId} not found");
        }

        // Ensure customer exists in Stripe
        var customerId = await GetOrCreateCustomerAsync(organisation);

        // Create portal session
        var portalService = new BillingPortalSessionService(_stripeClient);
        var returnUrl = $"{_stripeOptions.CheckoutSuccessUrl}/organisation/{organisationId}/subscription";

        var options = new BillingPortalSessionOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        };

        var session = await portalService.CreateAsync(options);

        _logger.LogInformation(
            "Customer portal session created: {SessionId} for organisation {OrganisationId}",
            session.Id, organisationId);

        return session.Url;
    }

    public async Task HandleWebhookEventAsync(string json, string signature)
    {
        // Step 1: Verify webhook signature
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signature,
                _stripeOptions.WebhookSecret
            );

            _logger.LogInformation(
                "Webhook event received: {EventType}, ID: {EventId}",
                stripeEvent.Type, stripeEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook signature verification failed");
            throw new InvalidOperationException("Invalid webhook signature", ex);
        }

        // Step 2: Check idempotency - have we already processed this event?
        var existingEvent = await _context.WebhookEvents
            .FirstOrDefaultAsync(e => e.StripeEventId == stripeEvent.Id);

        if (existingEvent != null)
        {
            _logger.LogInformation(
                "Webhook event {EventId} already processed at {ProcessedAt}, skipping",
                stripeEvent.Id, existingEvent.ProcessedAt);
            return; // Already processed, safe to skip
        }

        // Step 3: Process event based on type
        try
        {
            switch (stripeEvent.Type)
            {
                case EventTypes.CheckoutSessionCompleted:
                    await HandleCheckoutSessionCompletedAsync(stripeEvent);
                    break;

                case EventTypes.InvoicePaid:
                    await HandleInvoicePaidAsync(stripeEvent);
                    break;

                case EventTypes.CustomerSubscriptionUpdated:
                    await HandleSubscriptionUpdatedAsync(stripeEvent);
                    break;

                case EventTypes.CustomerSubscriptionDeleted:
                    await HandleSubscriptionDeletedAsync(stripeEvent);
                    break;

                case EventTypes.InvoicePaymentFailed:
                    await HandleInvoicePaymentFailedAsync(stripeEvent);
                    break;

                default:
                    _logger.LogInformation(
                        "Unhandled webhook event type: {EventType}",
                        stripeEvent.Type);
                    break;
            }

            // Step 4: Store event ID to mark as processed
            _context.WebhookEvents.Add(new Domain.Organisations.WebhookEvent
            {
                Id = Guid.NewGuid(),
                StripeEventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                ProcessedAt = DateTimeOffset.UtcNow,
                Payload = json // Store for debugging
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Webhook event {EventId} processed successfully",
                stripeEvent.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing webhook event {EventId} of type {EventType}",
                stripeEvent.Id, stripeEvent.Type);
            throw;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session == null)
        {
            _logger.LogWarning("Checkout session data is null");
            return;
        }

        _logger.LogInformation(
            "Processing checkout.session.completed for session {SessionId}",
            session.Id);

        // Get organisation ID from metadata
        if (!session.Metadata.TryGetValue("organisation_id", out var orgIdString) ||
            !Guid.TryParse(orgIdString, out var organisationId))
        {
            _logger.LogError(
                "Invalid or missing organisation_id in checkout session {SessionId} metadata",
                session.Id);
            return;
        }

        // Fetch subscription from Stripe to get latest state
        if (string.IsNullOrEmpty(session.SubscriptionId))
        {
            _logger.LogWarning(
                "No subscription ID in checkout session {SessionId}",
                session.Id);
            return;
        }

        var subscriptionService = new SubscriptionService(_stripeClient);
        var stripeSubscription = await subscriptionService.GetAsync(session.SubscriptionId);

        // Update local subscription
        var organisation = await _context.Organisations
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.Id == organisationId);

        if (organisation?.Subscription == null)
        {
            _logger.LogError(
                "Organisation {OrganisationId} or subscription not found",
                organisationId);
            return;
        }

        // Convert trial to active subscription
        organisation.Subscription.Status = Domain.Organisations.SubscriptionStatus.Active;
        organisation.Subscription.StripeSubscriptionId = stripeSubscription.Id;
        organisation.Subscription.PaidSeats = (int)(stripeSubscription.Items.Data.FirstOrDefault()?.Quantity ?? 0);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Converted trial to active subscription for organisation {OrganisationId}",
            organisationId);
    }

    private async Task HandleInvoicePaidAsync(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;

        if (invoice == null || string.IsNullOrEmpty(invoice.Parent.SubscriptionDetails.SubscriptionId))
        {
            _logger.LogWarning("Invoice data is null or missing subscription ID");
            return;
        }

        var subscriptionId = invoice.Parent.SubscriptionDetails.SubscriptionId!;

        _logger.LogInformation(
            "Processing invoice.paid for subscription {SubscriptionId}",
            subscriptionId);

        // Fetch fresh subscription data from Stripe
        var subscriptionService = new SubscriptionService(_stripeClient);
        var stripeSubscription = await subscriptionService.GetAsync(subscriptionId);

        // Find organisation by Stripe subscription ID
        var subscription = await _context.Subscriptions
            .Include(s => s.Organisation)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (subscription == null)
        {
            _logger.LogWarning(
                "Local subscription not found for Stripe subscription {SubscriptionId}",
                subscriptionId);
            return;
        }

        // Update subscription status and seats
        subscription.Status = Domain.Organisations.SubscriptionStatus.Active;
        subscription.PaidSeats = (int)(stripeSubscription.Items.Data.FirstOrDefault()?.Quantity ?? subscription.PaidSeats);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated subscription for organisation {OrganisationId} - Status: Active, Seats: {Seats}",
            subscription.OrganisationId, subscription.PaidSeats);
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent)
    {
        var stripeSubscription = stripeEvent.Data.Object as Subscription;
        if (stripeSubscription == null)
        {
            _logger.LogWarning("Subscription data is null");
            return;
        }

        _logger.LogInformation(
            "Processing customer.subscription.updated for subscription {SubscriptionId}",
            stripeSubscription.Id);

        // Find local subscription by Stripe subscription ID
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (subscription == null)
        {
            // Subscription doesn't exist locally yet - create it
            // This handles out-of-order events (subscription.updated arrives before subscription.created)
            _logger.LogInformation(
                "Local subscription not found for Stripe subscription {SubscriptionId}, fetching organisation from metadata",
                stripeSubscription.Id);

            // Try to get organisation ID from subscription metadata
            if (stripeSubscription.Metadata.TryGetValue("organisation_id", out var orgIdString) &&
                Guid.TryParse(orgIdString, out var organisationId))
            {
                var organisation = await _context.Organisations
                    .Include(o => o.Subscription)
                    .FirstOrDefaultAsync(o => o.Id == organisationId);

                if (organisation?.Subscription != null)
                {
                    subscription = organisation.Subscription;
                }
            }

            if (subscription == null)
            {
                _logger.LogWarning(
                    "Cannot process subscription.updated - organisation not found for subscription {SubscriptionId}",
                    stripeSubscription.Id);
                return;
            }
        }

        // Update subscription details
        subscription.Status = stripeSubscription.Status switch
        {
            "active" => Domain.Organisations.SubscriptionStatus.Active,
            "past_due" => Domain.Organisations.SubscriptionStatus.PastDue,
            "canceled" => Domain.Organisations.SubscriptionStatus.Cancelled,
            _ => subscription.Status // Keep current status if unknown
        };

        subscription.StripeSubscriptionId = stripeSubscription.Id;
        subscription.PaidSeats = (int)(stripeSubscription.Items.Data.FirstOrDefault()?.Quantity ?? subscription.PaidSeats);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated subscription {SubscriptionId} - Status: {Status}, Seats: {Seats}",
            stripeSubscription.Id, subscription.Status, subscription.PaidSeats);
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent)
    {
        var stripeSubscription = stripeEvent.Data.Object as Subscription;
        if (stripeSubscription == null)
        {
            _logger.LogWarning("Subscription data is null");
            return;
        }

        _logger.LogInformation(
            "Processing customer.subscription.deleted for subscription {SubscriptionId}",
            stripeSubscription.Id);

        // Find local subscription
        var subscription = await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (subscription == null)
        {
            _logger.LogWarning(
                "Local subscription not found for Stripe subscription {SubscriptionId}",
                stripeSubscription.Id);
            return;
        }

        // Mark as cancelled
        subscription.Status = Domain.Organisations.SubscriptionStatus.Cancelled;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Marked subscription as cancelled for organisation {OrganisationId}",
            subscription.OrganisationId);
    }

    private async Task HandleInvoicePaymentFailedAsync(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null || string.IsNullOrEmpty(invoice.Parent.SubscriptionDetails.SubscriptionId))
        {
            _logger.LogWarning("Invoice data is null or missing subscription ID");
            return;
        }

        var subscriptionId = invoice.Parent.SubscriptionDetails.SubscriptionId!;

        _logger.LogInformation(
            "Processing invoice.payment_failed for subscription {SubscriptionId}",
            subscriptionId);

        // Find local subscription
        var subscription = await _context.Subscriptions
            .Include(s => s.Organisation)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (subscription == null)
        {
            _logger.LogWarning(
                "Local subscription not found for Stripe subscription {SubscriptionId}",
                subscriptionId);
            return;
        }

        // Mark as past due
        subscription.Status = Domain.Organisations.SubscriptionStatus.PastDue;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Marked subscription as past due for organisation {OrganisationId}",
            subscription.OrganisationId);

        // TODO: Send dunning email to organisation admin
        // This would integrate with your email service
    }
}