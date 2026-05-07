using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Billing;
using Stripe;

namespace ProjectCallisto.Stripe;

public class StripeBillingService : IBillingService
{
    private readonly IStripeClient _stripeClient;
    private readonly ILogger<StripeBillingService> _logger;
    
    
    public StripeBillingService(
        IStripeClient stripeClient,
        ILogger<StripeBillingService> logger)
    {
        _stripeClient = stripeClient;
        _logger = logger;
    }
    
    public Task<CheckoutResult> CreateCheckoutSessionAsync(Guid organisationId, int seatCount, BillingInterval billingInterval)
    {
        throw new NotImplementedException();
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