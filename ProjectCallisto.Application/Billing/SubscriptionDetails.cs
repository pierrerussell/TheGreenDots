namespace ProjectCallisto.Application.Billing;

public class SubscriptionDetails
{
    public string Status { get; set; } = string.Empty;
    public int PaidSeats { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public BillingInterval? BillingInterval { get; set; }
    public decimal? PricePerSeat { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public string? StripeSubscriptionId { get; set; }
}
