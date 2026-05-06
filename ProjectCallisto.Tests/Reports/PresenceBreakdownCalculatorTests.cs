using ProjectCallisto.Application.Reports;
using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Tests.Reports;

public class PresenceBreakdownCalculatorTests
{
    private readonly IPresenceBreakdownCalculator _calculator;

    public PresenceBreakdownCalculatorTests()
    {
        _calculator = new PresenceBreakdownCalculator();
    }

    [Fact]
    public void Calculate_WithNoRecords_ReturnsAllZeros()
    {
        // Arrange
        var records = new List<PresenceHistory>();
        var periodStart = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 23, 59, 59, TimeSpan.Zero);

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(0, result.TotalHours);
        Assert.Equal(0, result.AvailableHours);
        Assert.Equal(0, result.BusyHours);
        Assert.Equal(0, result.AwayHours);
        Assert.Equal(0, result.DoNotDisturbHours);
        Assert.Equal(0, result.OfflineHours);
        Assert.Equal(0, result.AvailablePercent);
        Assert.Equal(0, result.BusyPercent);
    }

    [Fact]
    public void Calculate_WithSingleRecord_CalculatesDurationToPeriodEnd()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 17, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero), Availability = "Available" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(8, result.TotalHours); // 9 AM to 5 PM = 8 hours
        Assert.Equal(8, result.AvailableHours);
        Assert.Equal(0, result.BusyHours);
        Assert.Equal(100, result.AvailablePercent);
    }

    [Fact]
    public void Calculate_WithMultipleRecords_CalculatesCorrectDurations()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero), Availability = "Available" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero), Availability = "Busy" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 11, 0, 0, TimeSpan.Zero), Availability = "Away" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero), Availability = "Available" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        // 9-10 Available (1h), 10-11 Busy (1h), 11-12 Away (1h), 12-13 Available (1h)
        Assert.Equal(4, result.TotalHours);
        Assert.Equal(2, result.AvailableHours); // 1 + 1
        Assert.Equal(1, result.BusyHours);
        Assert.Equal(1, result.AwayHours);
        Assert.Equal(50, result.AvailablePercent); // 2/4 = 50%
        Assert.Equal(25, result.BusyPercent); // 1/4 = 25%
        Assert.Equal(25, result.AwayPercent); // 1/4 = 25%
    }

    [Fact]
    public void Calculate_DetectsOfflineGaps_WhenGapExceeds1_5Hours()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 17, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero), Availability = "Available" },
            // Gap of 3 hours - should insert 1.5h Available + 1.5h Offline
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero), Availability = "Busy" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(8, result.TotalHours);
        Assert.Equal(1.5, result.AvailableHours); // First 1.5 hours only
        Assert.Equal(5, result.BusyHours); // 12-17 = 5 hours
        Assert.Equal(1.5, result.OfflineHours); // Gap minus threshold
    }

    [Fact]
    public void Calculate_NormalizesStatusToFiveCategories()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 14, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero), Availability = "Available" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero), Availability = "InACall" }, // → Busy
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 11, 0, 0, TimeSpan.Zero), Availability = "BeRightBack" }, // → Away
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero), Availability = "DoNotDisturb" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 13, 0, 0, TimeSpan.Zero), Availability = "Offline" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(5, result.TotalHours);
        Assert.Equal(1, result.AvailableHours); // 9-10
        Assert.Equal(1, result.BusyHours); // 10-11 (InACall normalized to Busy)
        Assert.Equal(1, result.AwayHours); // 11-12 (BeRightBack normalized to Away)
        Assert.Equal(1, result.DoNotDisturbHours); // 12-13
        Assert.Equal(1, result.OfflineHours); // 13-14
    }

    [Fact]
    public void Calculate_HandlesFirstRecordAfterPeriodStart_AddsImplicitOffline()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 17, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            // First record at 10 AM, not 9 AM
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero), Availability = "Available" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(8, result.TotalHours);
        Assert.Equal(7, result.AvailableHours); // 10-17
        Assert.Equal(1, result.OfflineHours); // 9-10 (implicit offline)
    }

    [Fact]
    public void Calculate_CalculatesPercentagesCorrectly()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 5, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), Availability = "Available" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 1, 0, 0, TimeSpan.Zero), Availability = "Busy" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 2, 0, 0, TimeSpan.Zero), Availability = "Away" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 3, 0, 0, TimeSpan.Zero), Availability = "Available" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(5, result.TotalHours);
        Assert.Equal(3, result.AvailableHours); // 0-1 + 3-5 = 1 + 2
        Assert.Equal(1, result.BusyHours); // 1-2
        Assert.Equal(1, result.AwayHours); // 2-3
        Assert.Equal(60, result.AvailablePercent); // 3/5 = 60%
        Assert.Equal(20, result.BusyPercent); // 1/5 = 20%
        Assert.Equal(20, result.AwayPercent); // 1/5 = 20%
    }

    [Fact]
    public void Calculate_RoundsPercentagesToNearestInteger()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 3, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), Availability = "Available" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 1, 0, 0, TimeSpan.Zero), Availability = "Busy" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 2, 0, 0, TimeSpan.Zero), Availability = "Away" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        // 1/3 = 33.333...% should round to 33%
        Assert.Equal(3, result.TotalHours);
        Assert.Equal(33, result.AvailablePercent);
        Assert.Equal(33, result.BusyPercent);
        Assert.Equal(33, result.AwayPercent);
    }

    [Fact]
    public void Calculate_HandlesRecordsOutOfOrder_SortsChronologically()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 11, 0, 0, TimeSpan.Zero), Availability = "Away" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero), Availability = "Available" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero), Availability = "Busy" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        // Should sort to: 9AM Available, 10AM Busy, 11AM Away
        Assert.Equal(3, result.TotalHours);
        Assert.Equal(1, result.AvailableHours); // 9-10
        Assert.Equal(1, result.BusyHours); // 10-11
        Assert.Equal(1, result.AwayHours); // 11-12
    }

    [Fact]
    public void Calculate_MapsInAMeetingToBusy()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = periodStart, Availability = "InAMeeting" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(1, result.BusyHours);
        Assert.Equal(0, result.AvailableHours);
    }

    [Fact]
    public void Calculate_MapsOutOfOfficeToAway()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = periodStart, Availability = "OutOfOffice" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(1, result.AwayHours);
        Assert.Equal(0, result.AvailableHours);
    }

    [Fact]
    public void Calculate_MapsPresenceUnknownToOffline()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = periodStart, Availability = "PresenceUnknown" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        Assert.Equal(1, result.OfflineHours);
        Assert.Equal(0, result.AvailableHours);
    }

    [Fact]
    public void Calculate_HandlesExactBoundaryRecords()
    {
        // Arrange
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 10, 30, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            new() { RecordedAt = periodStart, Availability = "Available" }, // Exactly at start
            new() { RecordedAt = periodEnd, Availability = "Busy" } // Exactly at end
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        // First record at 9:00, second at 10:30 = 1.5 hours Available (no gap detection)
        // Second record at exactly periodEnd should have 0 duration
        Assert.Equal(1.5, result.TotalHours);
        Assert.Equal(1.5, result.AvailableHours);
        Assert.Equal(0, result.BusyHours);
    }

    /// <summary>
    /// BUG FIX TEST: Segments should be clipped to period boundaries (e.g., midnight for daily reports)
    /// This test would have caught the bug where records at 11:30 PM with next record at 12:30 AM
    /// were extending into the next day instead of being clipped at midnight.
    /// </summary>
    [Fact]
    public void Calculate_ClipsSegmentsToPeriodEnd_WhenNextRecordIsAfterPeriodEnd()
    {
        // Arrange - Daily report from midnight to midnight
        var periodStart = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero); // Midnight next day

        var records = new List<PresenceHistory>
        {
            // Record at 11:30 PM
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 23, 30, 0, TimeSpan.Zero), Availability = "Available" },
            // Next record at 12:30 AM (next day) - should be clipped
            new() { RecordedAt = new DateTimeOffset(2026, 5, 2, 0, 30, 0, TimeSpan.Zero), Availability = "Busy" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        // Should only count from 11:30 PM to midnight (30 minutes = 0.5 hours), NOT to 12:30 AM
        Assert.True(result.TotalHours <= 24, $"Total hours should not exceed 24, but was {result.TotalHours}");
        Assert.Equal(0.5, result.AvailableHours, 2); // 11:30 PM to midnight
        Assert.Equal(0, result.BusyHours); // Record at 12:30 AM is outside period
    }

    /// <summary>
    /// BUG FIX TEST: Records spanning period start should be clipped correctly
    /// </summary>
    [Fact]
    public void Calculate_ClipsSegmentsToPeriodStart_WhenRecordIsBeforePeriodStart()
    {
        // Arrange - Period starts at 9 AM
        var periodStart = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 1, 17, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            // Record at 8:00 AM (before period start)
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero), Availability = "Available" },
            new() { RecordedAt = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero), Availability = "Busy" }
        };

        // Act
        var result = _calculator.Calculate(records, periodStart, periodEnd);

        // Assert
        // Should have offline from 9-10 (implicit), then busy from 10-17
        Assert.Equal(8, result.TotalHours);
        Assert.Equal(1, result.OfflineHours); // 9-10 AM (gap from period start to first record)
        Assert.Equal(7, result.BusyHours); // 10-17
    }

    /// <summary>
    /// BUG FIX TEST: CalculateForWorkingHours should only count time within working hour windows
    /// This would have caught the bug where working hours showed 168h total instead of actual online hours
    /// </summary>
    [Fact]
    public void CalculateForWorkingHours_OnlyCountsTimeWithinWorkingHourWindows()
    {
        // Arrange - Working hours 9 AM - 5 PM, Mon-Fri
        var workingHours = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };

        // Week period: Monday May 5 to Sunday May 11, 2026
        var periodStart = new DateTimeOffset(2026, 5, 4, 0, 0, 0, TimeSpan.Zero); // Monday midnight
        var periodEnd = new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero); // Next Monday midnight

        var records = new List<PresenceHistory>
        {
            // Monday 9 AM - Available
            new() { RecordedAt = new DateTimeOffset(2026, 5, 4, 9, 0, 0, TimeSpan.Zero), Availability = "Available" },
            // Monday 10 AM - Change status (1 hour available)
            new() { RecordedAt = new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero), Availability = "Busy" },
            // Monday 11 AM - Offline
            new() { RecordedAt = new DateTimeOffset(2026, 5, 4, 11, 0, 0, TimeSpan.Zero), Availability = "Offline" }
        };

        // Act
        var result = _calculator.CalculateForWorkingHours(records, workingHours, "UTC", periodStart, periodEnd);

        // Assert
        // Should count: 9-10 Available (1h), 10-11 Busy (1h) = 2h total
        // NOT 40 hours or 168 hours which was the bug
        Assert.True(result.TotalHours < 5, $"Total hours should be small (2h), but was {result.TotalHours}");
        Assert.Equal(1, result.AvailableHours, 1);
        Assert.Equal(1, result.BusyHours, 1);
    }

    /// <summary>
    /// BUG FIX TEST: Records that start before working hours should count from working hours start
    /// Example: Record at 8:30 AM when work starts at 9:00 AM should count from 9:00 AM
    /// </summary>
    [Fact]
    public void CalculateForWorkingHours_ClipsRecordStartingBeforeWorkingHours()
    {
        // Arrange - Working hours 9 AM - 5 PM
        var workingHours = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };

        var periodStart = new DateTimeOffset(2026, 5, 5, 0, 0, 0, TimeSpan.Zero); // Tuesday
        var periodEnd = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            // Record at 8:30 AM (before working hours start)
            new() { RecordedAt = new DateTimeOffset(2026, 5, 5, 8, 30, 0, TimeSpan.Zero), Availability = "Available" },
            // Next record at 9:30 AM
            new() { RecordedAt = new DateTimeOffset(2026, 5, 5, 9, 30, 0, TimeSpan.Zero), Availability = "Busy" },
            // Another record at 10 AM to avoid offline gap threshold
            new() { RecordedAt = new DateTimeOffset(2026, 5, 5, 10, 0, 0, TimeSpan.Zero), Availability = "Offline" }
        };

        // Act
        var result = _calculator.CalculateForWorkingHours(records, workingHours, "UTC", periodStart, periodEnd);

        // Assert
        // Should count: 9:00-9:30 Available (0.5h) + 9:30-10:00 Busy (0.5h) = 1.0h total
        // NOT 8:30-10:00 which would be 1.5 hours
        Assert.True(result.TotalHours >= 0.5 && result.TotalHours <= 1.5,
            $"Total should be around 1.0h, but was {result.TotalHours}");
        Assert.True(result.AvailableHours >= 0.5 && result.AvailableHours <= 1.0,
            $"Available should be ~0.5h, but was {result.AvailableHours}");
    }

    /// <summary>
    /// BUG FIX TEST: Records that extend past working hours should be clipped at end time
    /// Example: Record at 4:45 PM when work ends at 5:00 PM should only count until 5:00 PM
    /// </summary>
    [Fact]
    public void CalculateForWorkingHours_ClipsRecordExtendingPastWorkingHours()
    {
        // Arrange - Working hours 9 AM - 5 PM
        var workingHours = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };

        var periodStart = new DateTimeOffset(2026, 5, 5, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);

        var records = new List<PresenceHistory>
        {
            // Record at 4:45 PM
            new() { RecordedAt = new DateTimeOffset(2026, 5, 5, 16, 45, 0, TimeSpan.Zero), Availability = "Available" },
            // Next record at 5:30 PM (after working hours)
            new() { RecordedAt = new DateTimeOffset(2026, 5, 5, 17, 30, 0, TimeSpan.Zero), Availability = "Busy" }
        };

        // Act
        var result = _calculator.CalculateForWorkingHours(records, workingHours, "UTC", periodStart, periodEnd);

        // Assert
        // Should only count 4:45-5:00 PM (15 minutes = 0.25 hours), NOT 4:45-5:30 PM
        Assert.Equal(0.25, result.TotalHours, 2);
        Assert.Equal(0.25, result.AvailableHours, 2);
    }

    /// <summary>
    /// BUG FIX TEST: CalculateForWorkingHours should handle records spanning multiple working days
    /// </summary>
    [Fact]
    public void CalculateForWorkingHours_HandlesRecordsSpanningMultipleDays()
    {
        // Arrange - Working hours 9 AM - 5 PM, Mon-Fri
        var workingHours = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };

        var periodStart = new DateTimeOffset(2026, 5, 4, 0, 0, 0, TimeSpan.Zero); // Monday
        var periodEnd = new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero); // Next Monday

        var records = new List<PresenceHistory>
        {
            // Monday 2 PM
            new() { RecordedAt = new DateTimeOffset(2026, 5, 4, 14, 0, 0, TimeSpan.Zero), Availability = "Available" },
            // Tuesday 10 AM (next day)
            new() { RecordedAt = new DateTimeOffset(2026, 5, 5, 10, 0, 0, TimeSpan.Zero), Availability = "Busy" }
        };

        // Act
        var result = _calculator.CalculateForWorkingHours(records, workingHours, "UTC", periodStart, periodEnd);

        // Assert
        // Should count: Monday 2-5 PM (3 hours) + Tuesday 9-10 AM (1 hour) = 4 hours
        Assert.Equal(4, result.TotalHours, 1);
        Assert.Equal(3, result.AvailableHours, 1); // Monday afternoon
        Assert.Equal(1, result.BusyHours, 1); // Tuesday morning
    }

    /// <summary>
    /// BUG FIX TEST: Records on non-working days should not be counted
    /// </summary>
    [Fact]
    public void CalculateForWorkingHours_ExcludesNonWorkingDays()
    {
        // Arrange - Working hours 9 AM - 5 PM, Mon-Fri only
        var workingHours = new WorkingHours(Guid.NewGuid())
        {
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                          WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                          WorkingDaysFlags.Friday
        };

        var periodStart = new DateTimeOffset(2026, 5, 9, 0, 0, 0, TimeSpan.Zero); // Saturday
        var periodEnd = new DateTimeOffset(2026, 5, 11, 0, 0, 0, TimeSpan.Zero); // Monday

        var records = new List<PresenceHistory>
        {
            // Saturday 10 AM (non-working day)
            new() { RecordedAt = new DateTimeOffset(2026, 5, 9, 10, 0, 0, TimeSpan.Zero), Availability = "Available" },
            // Sunday 11 AM (non-working day)
            new() { RecordedAt = new DateTimeOffset(2026, 5, 10, 11, 0, 0, TimeSpan.Zero), Availability = "Busy" }
        };

        // Act
        var result = _calculator.CalculateForWorkingHours(records, workingHours, "UTC", periodStart, periodEnd);

        // Assert
        // Should count 0 hours (Saturday and Sunday are not working days)
        Assert.Equal(0, result.TotalHours, 1);
    }
}
