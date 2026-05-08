namespace ProjectCallisto.Application.Billing;

public interface IBillingService
{
    Task<CheckoutResult> CreateCheckoutSessionAsync(
        Guid organisationId, int seatCount, BillingInterval billingInterval);

    // Subscription details
    Task<SubscriptionDetails> GetSubscriptionAsync(Guid organisationId);

    // Customer Portal
    Task<string> CreateCustomerPortalSessionAsync(Guid organisationId);

    // Webhooks
    Task HandleWebhookEventAsync(string json, string signature);


}