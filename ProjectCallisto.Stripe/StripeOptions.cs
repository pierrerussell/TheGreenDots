namespace ProjectCallisto.Stripe;

public class StripeOptions
{
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string CheckoutSuccessUrl { get; set; } = string.Empty;
    public string CheckoutCancelUrl { get; set; } = string.Empty;
}