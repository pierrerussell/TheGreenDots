namespace ProjectCallisto.Application.Reports;

public record ReportCalculationJob
{
    public Guid EmailReportSettingsId { get; init; }
    public Guid OrganisationId { get; init; }
    public string OrganisationName { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty; // "Daily", "Weekly", "Monthly"
}