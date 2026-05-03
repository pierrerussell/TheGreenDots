using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Application.Reports;

public class InsightDetectionService : IInsightDetectionService
{
    private const double OVERTIME_THRESHOLD_HOURS = 5.0;
    private const double OVERTIME_ALERT_THRESHOLD_HOURS = 10.0;
    private const double HIGH_AWAY_PERCENTAGE = 0.30;
    private const double HIGH_OFFLINE_PERCENTAGE = 0.25;

    public List<PresenceInsight> DetectInsights(
        TimeBreakdown workingHours,
        TimeBreakdown fullPeriod,
        WorkingHours config)
    {
        var insights = new List<PresenceInsight>();

        // Calculate overtime
        var overtimeHours = fullPeriod.TotalHours - workingHours.TotalHours;

        // Detect High Overtime
        if (overtimeHours > OVERTIME_THRESHOLD_HOURS)
        {
            var severity = overtimeHours > OVERTIME_ALERT_THRESHOLD_HOURS
                ? InsightSeverity.Alert
                : InsightSeverity.Warning;

            insights.Add(new PresenceInsight
            {
                Type = InsightType.HighOvertime,
                Message = $"Logged {overtimeHours:F1} hours outside working hours",
                Severity = severity,
                Value = overtimeHours
            });
        }

        // Detect High Away Time (only if there are working hours to analyze)
        if (workingHours.TotalHours > 0)
        {
            var awayPercentage = workingHours.AwayHours / workingHours.TotalHours;
            if (awayPercentage > HIGH_AWAY_PERCENTAGE)
            {
                insights.Add(new PresenceInsight
                {
                    Type = InsightType.HighAwayTime,
                    Message = $"Away for {awayPercentage * 100:F0}% of working hours",
                    Severity = InsightSeverity.Warning,
                    Value = awayPercentage * 100
                });
            }
        }

        // Detect High Offline During Working Hours
        var expectedWorkingHours = CalculateExpectedWorkingHours(config);
        if (expectedWorkingHours > 0)
        {
            var offlinePercentage = workingHours.OfflineHours / expectedWorkingHours;
            if (offlinePercentage > HIGH_OFFLINE_PERCENTAGE)
            {
                insights.Add(new PresenceInsight
                {
                    Type = InsightType.HighOfflineDuringWorkingHours,
                    Message = $"Offline for {offlinePercentage * 100:F0}% of expected working hours",
                    Severity = InsightSeverity.Warning,
                    Value = offlinePercentage * 100
                });
            }
        }

        return insights;
    }

    private double CalculateExpectedWorkingHours(WorkingHours config)
    {
        // Count number of working days
        var workingDays = 0;
        if (config.WorkingDays.HasFlag(WorkingDaysFlags.Monday)) workingDays++;
        if (config.WorkingDays.HasFlag(WorkingDaysFlags.Tuesday)) workingDays++;
        if (config.WorkingDays.HasFlag(WorkingDaysFlags.Wednesday)) workingDays++;
        if (config.WorkingDays.HasFlag(WorkingDaysFlags.Thursday)) workingDays++;
        if (config.WorkingDays.HasFlag(WorkingDaysFlags.Friday)) workingDays++;
        if (config.WorkingDays.HasFlag(WorkingDaysFlags.Saturday)) workingDays++;
        if (config.WorkingDays.HasFlag(WorkingDaysFlags.Sunday)) workingDays++;

        // Calculate hours per day (EndTime - StartTime)
        var hoursPerDay = (config.EndTime - config.StartTime).TotalHours;

        // Total expected hours = working days × hours per day
        return workingDays * hoursPerDay;
    }
}
