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
