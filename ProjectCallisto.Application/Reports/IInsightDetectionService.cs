using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Application.Reports;

public interface IInsightDetectionService
{
    List<PresenceInsight> DetectInsights(
        TimeBreakdown workingHours,
        TimeBreakdown fullPeriod,
        WorkingHours config);
}
