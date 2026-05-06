using ProjectCallisto.Application.Reports;
using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Tests.Reports;

public class InsightDetectionServiceTests
{
    private readonly IInsightDetectionService _service;

    public InsightDetectionServiceTests()
    {
        _service = new InsightDetectionService();
    }

    [Fact]
    public void DetectInsights_WithNormalActivity_ReturnsNoInsights()
    {
        // Arrange
        var workingHours = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 30,
            BusyHours = 8,
            AwayHours = 2,
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 42,
            AvailableHours = 30,
            BusyHours = 10,
            AwayHours = 2,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Empty(insights);
    }

    [Fact]
    public void DetectInsights_WithHighOvertimeWarning_ReturnsOvertimeWarning()
    {
        // Arrange - 7 hours overtime (>5 but <10)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 40,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 47,
            AvailableHours = 47,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Single(insights);
        Assert.Equal(InsightType.HighOvertime, insights[0].Type);
        Assert.Equal(InsightSeverity.Warning, insights[0].Severity);
        Assert.Equal(7, insights[0].Value);
        Assert.Contains("7.0 hours outside working hours", insights[0].Message);
    }

    [Fact]
    public void DetectInsights_WithHighOvertimeAlert_ReturnsOvertimeAlert()
    {
        // Arrange - 12 hours overtime (>10)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 40,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 52,
            AvailableHours = 52,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Single(insights);
        Assert.Equal(InsightType.HighOvertime, insights[0].Type);
        Assert.Equal(InsightSeverity.Alert, insights[0].Severity);
        Assert.Equal(12, insights[0].Value);
        Assert.Contains("12.0 hours outside working hours", insights[0].Message);
    }

    [Fact]
    public void DetectInsights_WithExactOvertimeThreshold_NoInsight()
    {
        // Arrange - Exactly 5 hours overtime (not > 5)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 40,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 45,
            AvailableHours = 45,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Empty(insights);
    }

    [Fact]
    public void DetectInsights_WithHighAwayTime_ReturnsAwayWarning()
    {
        // Arrange - 40% away time (>30%)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 20,
            BusyHours = 4,
            AwayHours = 16, // 16/40 = 40%
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 20,
            BusyHours = 4,
            AwayHours = 16,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Single(insights);
        Assert.Equal(InsightType.HighAwayTime, insights[0].Type);
        Assert.Equal(InsightSeverity.Warning, insights[0].Severity);
        Assert.Equal(40, insights[0].Value);
        Assert.Contains("Away for 40% of working hours", insights[0].Message);
    }

    [Fact]
    public void DetectInsights_WithExactAwayThreshold_NoInsight()
    {
        // Arrange - Exactly 30% away time (not > 30%)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 28,
            BusyHours = 0,
            AwayHours = 12, // 12/40 = 30%
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 28,
            BusyHours = 0,
            AwayHours = 12,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Empty(insights);
    }

    [Fact]
    public void DetectInsights_WithHighOfflineTime_ReturnsOfflineWarning()
    {
        // Arrange - 30% offline time (>25%)
        // Expected working hours = 5 days × 8 hours = 40 hours
        var workingHours = new TimeBreakdown
        {
            TotalHours = 28,
            AvailableHours = 20,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 12 // 12/40 = 30%
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 28,
            AvailableHours = 20,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 12
        };

        var config = CreateWorkingHoursConfig(); // Mon-Fri, 9-5 = 40 expected hours

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Single(insights);
        Assert.Equal(InsightType.HighOfflineDuringWorkingHours, insights[0].Type);
        Assert.Equal(InsightSeverity.Warning, insights[0].Severity);
        Assert.Equal(30, insights[0].Value);
        Assert.Contains("Offline for 30% of expected working hours", insights[0].Message);
    }

    [Fact]
    public void DetectInsights_WithMultipleIssues_ReturnsAllInsights()
    {
        // Arrange - High overtime, high away, high offline
        var workingHours = new TimeBreakdown
        {
            TotalHours = 30, // Only worked 30 out of 40 expected
            AvailableHours = 10,
            BusyHours = 5,
            AwayHours = 12, // 12/30 = 40% away
            OfflineHours = 15 // 15/40 = 37.5% offline
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 42, // 12 hours overtime
            AvailableHours = 20,
            BusyHours = 10,
            AwayHours = 12,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Equal(3, insights.Count);
        Assert.Contains(insights, i => i.Type == InsightType.HighOvertime);
        Assert.Contains(insights, i => i.Type == InsightType.HighAwayTime);
        Assert.Contains(insights, i => i.Type == InsightType.HighOfflineDuringWorkingHours);
    }

    [Fact]
    public void DetectInsights_WithZeroWorkingHours_NoAwayInsight()
    {
        // Arrange - Zero working hours (edge case)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 0,
            AvailableHours = 0,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 10,
            AvailableHours = 10,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        // Should detect overtime (10 hours) but NOT high away (division by zero protection)
        Assert.Single(insights);
        Assert.Equal(InsightType.HighOvertime, insights[0].Type);
    }

    [Fact]
    public void DetectInsights_WithCustomWorkingDays_CalculatesExpectedHoursCorrectly()
    {
        // Arrange - Working only 3 days: Mon, Wed, Fri
        var workingHours = new TimeBreakdown
        {
            TotalHours = 20,
            AvailableHours = 20,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 7 // 7/24 = 29% offline (>25%)
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 20,
            AvailableHours = 20,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 7
        };

        var config = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Wednesday | WorkingDaysFlags.Friday
        };
        // Expected hours = 3 days × 8 hours = 24 hours

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Single(insights);
        Assert.Equal(InsightType.HighOfflineDuringWorkingHours, insights[0].Type);
    }

    [Fact]
    public void DetectInsights_WithNonStandardWorkingHours_CalculatesCorrectly()
    {
        // Arrange - 10-hour workdays (8 AM to 6 PM)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 40,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 15 // 15/50 = 30% offline (>25%)
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 40,
            BusyHours = 0,
            AwayHours = 0,
            OfflineHours = 15
        };

        var config = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(18, 0), // 10 hours per day
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
        // Expected hours = 5 days × 10 hours = 50 hours

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        Assert.Single(insights);
        Assert.Equal(InsightType.HighOfflineDuringWorkingHours, insights[0].Type);
        Assert.Equal(30, insights[0].Value);
    }

    /// <summary>
    /// BUG FIX TEST: Overtime should be calculated using only ONLINE hours, not total hours including offline
    /// This would have caught the bug where it showed "Logged 123 hours outside working hours"
    /// when it should have been 0 (because workingHours.TotalHours and fullPeriod.TotalHours both included offline time)
    /// </summary>
    [Fact]
    public void DetectInsights_CalculatesOvertimeUsingOnlineHours_NotTotalHours()
    {
        // Arrange - Scenario from the bug report
        // User worked 2 hours online total, both during working hours
        // Working hours window: 45 hours (9-5, Mon-Fri)
        // Full week: 168 hours
        var workingHours = new TimeBreakdown
        {
            TotalHours = 45, // Includes offline time (working hour window)
            AvailableHours = 0.2,
            BusyHours = 1.9,
            AwayHours = 0,
            DoNotDisturbHours = 0,
            OfflineHours = 42.9 // 45 - 2.1 = 42.9
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 168, // Entire week including offline
            AvailableHours = 0.2,
            BusyHours = 1.9,
            AwayHours = 0,
            DoNotDisturbHours = 0,
            OfflineHours = 165.9 // 168 - 2.1 = 165.9
        };

        var config = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        // OLD BUG: Would calculate 168 - 45 = 123 hours overtime
        // NEW FIX: Should calculate (168 - 165.9) - (45 - 42.9) = 2.1 - 2.1 = 0 hours overtime
        var overtimeInsight = insights.FirstOrDefault(i => i.Type == InsightType.HighOvertime);
        Assert.Null(overtimeInsight); // Should be null because overtime is 0
    }

    /// <summary>
    /// BUG FIX TEST: Overtime should correctly identify time spent online OUTSIDE working hours
    /// </summary>
    [Fact]
    public void DetectInsights_CorrectlyCalculatesOvertimeWhenUserWorksOutsideWorkingHours()
    {
        // Arrange - User worked 40 hours during working hours + 8 hours outside working hours
        var workingHours = new TimeBreakdown
        {
            TotalHours = 40,
            AvailableHours = 30,
            BusyHours = 10,
            AwayHours = 0,
            DoNotDisturbHours = 0,
            OfflineHours = 0 // All time during working hours was online
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 48,
            AvailableHours = 35,
            BusyHours = 13,
            AwayHours = 0,
            DoNotDisturbHours = 0,
            OfflineHours = 0 // All time was online (48 hours total)
        };

        var config = CreateWorkingHoursConfig();

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        // Overtime = (48 - 0) - (40 - 0) = 8 hours
        var overtimeInsight = insights.FirstOrDefault(i => i.Type == InsightType.HighOvertime);
        Assert.NotNull(overtimeInsight);
        Assert.Equal(8, overtimeInsight.Value);
        Assert.Contains("8.0 hours outside working hours", overtimeInsight.Message);
    }

    /// <summary>
    /// BUG FIX TEST: Offline percentage should be calculated as (expected - actual online) / expected
    /// Not just using workingHours.OfflineHours directly
    /// </summary>
    [Fact]
    public void DetectInsights_CalculatesOfflinePercentageCorrectly()
    {
        // Arrange - User worked 2 hours out of 45 expected hours
        // Expected working hours: 45 (9-5, Mon-Fri = 5 days × 9 hours)
        // Actual online hours during working hours: 2
        // Offline hours: 45 - 2 = 43 hours = 95.6%
        var workingHours = new TimeBreakdown
        {
            TotalHours = 2, // Only 2 hours of actual data
            AvailableHours = 0.2,
            BusyHours = 1.8,
            AwayHours = 0,
            DoNotDisturbHours = 0,
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 2,
            AvailableHours = 0.2,
            BusyHours = 1.8,
            AwayHours = 0,
            DoNotDisturbHours = 0,
            OfflineHours = 0
        };

        var config = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0), // 9 hours per day
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
        // Expected: 5 days × 9 hours = 45 hours

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        // Offline percentage = (45 - 2) / 45 = 43 / 45 = 95.6% ≈ 96%
        var offlineInsight = insights.FirstOrDefault(i => i.Type == InsightType.HighOfflineDuringWorkingHours);
        Assert.NotNull(offlineInsight);
        Assert.InRange(offlineInsight.Value, 95, 96); // Should be ~96%
        Assert.Contains("Offline for", offlineInsight.Message);
    }

    /// <summary>
    /// BUG FIX TEST: When user has minimal presence but high offline time in breakdown,
    /// offline percentage should be based on expected hours minus actual online hours
    /// </summary>
    [Fact]
    public void DetectInsights_HandlesLowPresenceWithHighOfflineCorrectly()
    {
        // Arrange - User barely present (5 hours out of 40 expected)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 5,
            AvailableHours = 3,
            BusyHours = 2,
            AwayHours = 0,
            DoNotDisturbHours = 0,
            OfflineHours = 0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 5,
            AvailableHours = 3,
            BusyHours = 2,
            AwayHours = 0,
            DoNotDisturbHours = 0,
            OfflineHours = 0
        };

        var config = CreateWorkingHoursConfig(); // 40 expected hours

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        // Offline percentage = (40 - 5) / 40 = 35 / 40 = 87.5% ≈ 88%
        var offlineInsight = insights.FirstOrDefault(i => i.Type == InsightType.HighOfflineDuringWorkingHours);
        Assert.NotNull(offlineInsight);
        Assert.InRange(offlineInsight.Value, 87, 88);
    }

    /// <summary>
    /// BUG FIX TEST: Full scenario matching the bug report
    /// </summary>
    [Fact]
    public void DetectInsights_BugReport_PeifenScenario()
    {
        // Arrange - Peifen's actual data from bug report
        // Working hours: 2.0h total (0.2h available + 1.9h busy)
        // Full week: 2.0h total (same as working hours)
        // Expected: 45h (9 hours/day × 5 days)
        var workingHours = new TimeBreakdown
        {
            TotalHours = 2.0,
            AvailableHours = 0.2,
            BusyHours = 1.9,
            AwayHours = 0.0,
            DoNotDisturbHours = 0.0,
            OfflineHours = 0.0
        };

        var fullPeriod = new TimeBreakdown
        {
            TotalHours = 2.0,
            AvailableHours = 0.2,
            BusyHours = 1.9,
            AwayHours = 0.0,
            DoNotDisturbHours = 0.0,
            OfflineHours = 0.0
        };

        var config = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0), // 9 hours/day
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };

        // Act
        var insights = _service.DetectInsights(workingHours, fullPeriod, config);

        // Assert
        // 1. Should NOT show "Logged 123 hours outside working hours"
        var overtimeInsight = insights.FirstOrDefault(i => i.Type == InsightType.HighOvertime);
        if (overtimeInsight != null)
        {
            Assert.NotEqual(123, overtimeInsight.Value); // Bug was showing 123
            Assert.True(overtimeInsight.Value < 5); // Should be 0 or very small
        }

        // 2. Should show offline for ~95% of expected working hours
        var offlineInsight = insights.FirstOrDefault(i => i.Type == InsightType.HighOfflineDuringWorkingHours);
        Assert.NotNull(offlineInsight);
        // (45 - 2.1) / 45 = 95.3%
        Assert.InRange(offlineInsight.Value, 94, 96);
        Assert.Contains("Offline for 95% of expected working hours", offlineInsight.Message);
    }

    private WorkingHours CreateWorkingHoursConfig()
    {
        return new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };
    }
}
