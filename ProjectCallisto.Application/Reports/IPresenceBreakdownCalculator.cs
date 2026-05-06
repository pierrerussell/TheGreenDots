using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Application.Reports;

public interface IPresenceBreakdownCalculator
{
    TimeBreakdown Calculate(
        List<PresenceHistory> records,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd);

    /// <summary>
    /// Calculates time breakdown from records without filling gaps.
    /// Only counts actual online time from the records themselves.
    /// Useful for working hours where we don't want to count non-working time as offline.
    /// </summary>
    TimeBreakdown CalculateWithoutFillingGaps(
        List<PresenceHistory> records);

    /// <summary>
    /// Calculates time breakdown for records that overlap with working hours,
    /// clipping each segment to only count the portion within the working hour windows.
    /// Handles records that span working hour boundaries correctly.
    /// </summary>
    TimeBreakdown CalculateForWorkingHours(
        List<PresenceHistory> allRecords,
        WorkingHours workingHours,
        string timezone,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd);
}
