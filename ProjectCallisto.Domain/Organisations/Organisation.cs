namespace ProjectCallisto.Domain.Organisations;

public class Organisation
{
    private Organisation() {}

    public Organisation(string name, string microsoftTenantId, Guid connectionId, int trialSeats)
    {
        Id = Guid.NewGuid();
        Name = name;
        TenantId = microsoftTenantId;
        ActiveConnectionId = connectionId;
        CreatedAt = DateTimeOffset.Now;
        
        Subscription = new Subscription(Id, trialSeats); // Default to 999 seats for trial

    }
    
    public Guid Id { get;  set; }
    public string Name  { get;  set; }
    // Id of the microsoft entra tenant
    public string TenantId  { get;  set; }
    // Id of the Active Access token used to connect to this tenant
    public Guid ActiveConnectionId  { get;  set; }
    public string? StripeCustomerId  { get;  set; }
    public DateTimeOffset CreatedAt { get;  set; }

    public Subscription Subscription { get; set; } = null!;
}