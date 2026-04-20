using System.Text.Json.Serialization;

namespace ProjectCallisto.Domain.Organisations;

public class Subscription
{
    private Subscription() { }

    public Subscription(Guid organisationId, int seats)
    {
        Id = Guid.NewGuid();
        OrganisationId = organisationId;
        Status = SubscriptionStatus.Trial;
        PaidSeats = seats;
        TrialEndsAt = DateTimeOffset.UtcNow.AddDays(7);
        CreatedAt = DateTimeOffset.UtcNow;
    }
    
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public int PaidSeats { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTimeOffset  CreatedAt { get; set; }
    public DateTimeOffset? TrialEndsAt { get; set; }
    
    // Navigation property
    [JsonIgnore]
    public Organisation Organisation { get; set; } = null!;
}

public enum SubscriptionStatus
{
    Trial,
    Active,
    PastDue,
    Cancelled
}