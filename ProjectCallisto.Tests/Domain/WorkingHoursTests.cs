using FluentAssertions;
using ProjectCallisto.Domain.Organisations;
using Xunit;

namespace ProjectCallisto.Tests.Domain;

public class WorkingHoursTests
{
    private readonly Guid _organisationId = Guid.NewGuid();

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var workingHours = new WorkingHours(_organisationId);

        // Assert
        workingHours.OrganisationId.Should().Be(_organisationId);
        workingHours.StartTime.Should().Be(new TimeOnly(9, 0));
        workingHours.EndTime.Should().Be(new TimeOnly(17, 0));
        workingHours.WorkingDays.Should().Be(
            WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
            WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
            WorkingDaysFlags.Friday);
    }

    [Theory]
    [InlineData("Asia/Singapore", "2024-01-15 02:00:00Z", true)]  // 2 AM UTC = 10 AM SGT (UTC+8)
    [InlineData("Asia/Singapore", "2024-01-15 00:59:59Z", false)] // 00:59:59 UTC = 08:59:59 SGT (before start)
    [InlineData("Asia/Singapore", "2024-01-15 09:00:00Z", true)]  // 9 AM UTC = 5 PM SGT (inclusive)
    [InlineData("Asia/Singapore", "2024-01-15 09:00:01Z", false)] // 9:00:01 AM UTC = 5:00:01 PM SGT (after end)
    public void IsWithinWorkingHours_TimeRange_ReturnsExpectedResult(
        string timezone,
        string timestampStr,
        bool expected)
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId);
        var timestamp = DateTimeOffset.Parse(timestampStr);

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, timezone);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("2024-01-15", true)]  // Monday - working day
    [InlineData("2024-01-16", true)]  // Tuesday - working day
    [InlineData("2024-01-17", true)]  // Wednesday - working day
    [InlineData("2024-01-18", true)]  // Thursday - working day
    [InlineData("2024-01-19", true)]  // Friday - working day
    [InlineData("2024-01-20", false)] // Saturday - non-working day
    [InlineData("2024-01-21", false)] // Sunday - non-working day
    public void IsWithinWorkingHours_DayOfWeek_ReturnsExpectedResult(string dateStr, bool expected)
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId);
        var timestamp = DateTimeOffset.Parse($"{dateStr} 10:00:00", null, System.Globalization.DateTimeStyles.AssumeUniversal);

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, "UTC");

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsWithinWorkingHours_WithTimezoneConversion_Singapore_ReturnsCorrectResult()
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId);
        // 1 AM UTC = 9 AM SGT (UTC+8), Monday
        var timestamp = DateTimeOffset.Parse("2024-01-15 01:00:00Z");

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, "Asia/Singapore");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinWorkingHours_WithTimezoneConversion_NewYork_ReturnsCorrectResult()
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId);
        // 2 PM UTC = 9 AM EST (UTC-5), Monday
        var timestamp = DateTimeOffset.Parse("2024-01-15 14:00:00Z");

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, "America/New_York");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinWorkingHours_MidnightEdgeCase_ReturnsFalse()
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId);
        var timestamp = DateTimeOffset.Parse("2024-01-15 00:00:00Z");

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, "UTC");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinWorkingHours_EndOfDayEdgeCase_ReturnsFalse()
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId);
        var timestamp = DateTimeOffset.Parse("2024-01-15 23:59:59Z");

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, "UTC");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinWorkingHours_CustomWorkingDays_Saturday_ReturnsTrue()
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId)
        {
            WorkingDays = WorkingDaysFlags.Saturday | WorkingDaysFlags.Sunday
        };
        var timestamp = DateTimeOffset.Parse("2024-01-20 10:00:00Z"); // Saturday

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, "UTC");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinWorkingHours_CustomTimeRange_ReturnsTrue()
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId)
        {
            StartTime = new TimeOnly(6, 0),
            EndTime = new TimeOnly(14, 0)
        };
        var timestamp = DateTimeOffset.Parse("2024-01-15 13:00:00Z"); // Monday 1 PM

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, "UTC");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void WorkingDaysFlags_BitwiseOperations_WorkCorrectly()
    {
        // Arrange
        var flags = WorkingDaysFlags.Monday | WorkingDaysFlags.Friday;

        // Assert
        flags.HasFlag(WorkingDaysFlags.Monday).Should().BeTrue();
        flags.HasFlag(WorkingDaysFlags.Friday).Should().BeTrue();
        flags.HasFlag(WorkingDaysFlags.Tuesday).Should().BeFalse();
        flags.HasFlag(WorkingDaysFlags.Wednesday).Should().BeFalse();
    }

    [Fact]
    public void WorkingDaysFlags_AllDays_WorkCorrectly()
    {
        // Arrange
        var flags = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                    WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                    WorkingDaysFlags.Friday | WorkingDaysFlags.Saturday |
                    WorkingDaysFlags.Sunday;

        // Assert
        flags.HasFlag(WorkingDaysFlags.Monday).Should().BeTrue();
        flags.HasFlag(WorkingDaysFlags.Tuesday).Should().BeTrue();
        flags.HasFlag(WorkingDaysFlags.Wednesday).Should().BeTrue();
        flags.HasFlag(WorkingDaysFlags.Thursday).Should().BeTrue();
        flags.HasFlag(WorkingDaysFlags.Friday).Should().BeTrue();
        flags.HasFlag(WorkingDaysFlags.Saturday).Should().BeTrue();
        flags.HasFlag(WorkingDaysFlags.Sunday).Should().BeTrue();
    }

    [Theory]
    [InlineData("UTC", "2024-01-15 09:00:00Z", true)]
    [InlineData("UTC", "2024-01-15 17:00:00Z", true)]
    [InlineData("Europe/London", "2024-01-15 09:00:00Z", true)]  // 9 AM UTC = 9 AM GMT in winter
    [InlineData("Europe/London", "2024-01-15 08:59:59Z", false)] // 08:59:59 UTC = 08:59:59 GMT in winter (before start)
    public void IsWithinWorkingHours_DifferentTimezones_WorkCorrectly(
        string timezone,
        string timestampStr,
        bool expected)
    {
        // Arrange
        var workingHours = new WorkingHours(_organisationId);
        var timestamp = DateTimeOffset.Parse(timestampStr);

        // Act
        var result = workingHours.IsWithinWorkingHours(timestamp, timezone);

        // Assert
        result.Should().Be(expected);
    }
}
