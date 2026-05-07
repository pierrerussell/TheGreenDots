using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectCallisto.Application.Billing;
using ProjectCallisto.EfCore;
using Stripe;
using Stripe.Checkout;

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

        // Determine price lookup key based on billing interval
        var priceLookupKey = billingInterval == BillingInterval.Monthly
            ? "monthly_volume"
            : "annual_volume";

        var priceService = new PriceService(_stripeClient);
        var prices = await priceService.ListAsync(new PriceListOptions
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
        var sessionService = new SessionService(_stripeClient);
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

    public Task<string> CreateCustomerPortalSessionAsync(Guid organisationId)
    {
        throw new NotImplementedException();
    }

    public Task HandleWebhookEventAsync(string json, string signature)
    {
        throw new NotImplementedException();
    }
}