namespace ProjectCallisto.Domain.Organisations;

public class WebhookEvent
{
    public Guid Id { get; set; }
    public string StripeEventId { get; set; } = null!; // Unique constraint - prevents duplicate processing
    public string EventType { get; set; } = null!;
    public DateTimeOffset ProcessedAt { get; set; }
    public string? Payload { get; set; } // Optional: store full event JSON for debugging
}
