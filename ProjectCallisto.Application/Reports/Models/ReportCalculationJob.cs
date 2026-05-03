namespace ProjectCallisto.Application.Reports.Models;

public record ReportCalculationJob
{
    public Guid EmailReportSettingsId { get; init; }
    public Guid OrganisationId { get; init; }
    public string OrganisationName { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty; // "Daily", "Weekly", "Monthly"
    public List<EmailRecipientDto> Recipients { get; init; } = new();
}

public record EmailRecipientDto
{
    public string Email { get; init; } = string.Empty;
    public string? Name { get; init; }
}