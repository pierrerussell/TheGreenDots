namespace ProjectCallisto.Application.Reports.Models;

public record WeeklyReportData
{
    public Guid OrganisationId { get; init; }
    public string OrganisationName { get; init; } = string.Empty;
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public string Timezone { get; init; } = string.Empty;
    public List<EmployeePresenceBreakdown> Employees { get; init; } = new();
    public int TotalMembers { get; init; }
}
