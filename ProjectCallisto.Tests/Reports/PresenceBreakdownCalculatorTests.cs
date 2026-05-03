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
}
