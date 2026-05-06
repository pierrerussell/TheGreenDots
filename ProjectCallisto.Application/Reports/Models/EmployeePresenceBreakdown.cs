using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Application.Reports.Models;

public record EmployeePresenceBreakdown
{
    public Guid TenantMemberId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? JobTitle { get; init; }

    public TimeBreakdown WorkingHoursBreakdown { get; init; } = new();
    public TimeBreakdown FullWeekBreakdown { get; init; } = new();
    public double OvertimeHours { get; init; }

    public List<PresenceInsight> Insights { get; init; } = new();

    // Raw presence records for building chronological timelines
    public List<PresenceHistory> PresenceRecords { get; init; } = new();
}
