namespace ProjectCallisto.Domain.Organisations;

public class TenantMember
{
    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }
    // Microsoft Graph user ID
    public string MicrosoftUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? JobTitle { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
