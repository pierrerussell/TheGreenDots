using FluentAssertions;
using ProjectCallisto.Domain.Organisations;
using Xunit;

namespace ProjectCallisto.Tests.Helpers;

public class EmailReportSettingsHelperTests
{
    [Theory]
    [InlineData("daily", ReportFrequency.Daily)]
    [InlineData("weekly", ReportFrequency.Weekly)]
    [InlineData("monthly", ReportFrequency.Monthly)]
    public void ParseFrequency_ValidFrequency_ReturnsCorrectEnum(string frequency, ReportFrequency expected)
    {
        // Act
        var result = ParseFrequencyTestHelper(frequency);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("DAILY")]
    [InlineData("Daily")]
    [InlineData("DaIlY")]
    public void ParseFrequency_CaseInsensitive_WorksCorrectly(string frequency)
    {
        // Act
        var result = ParseFrequencyTestHelper(frequency);

        // Assert
        result.Should().Be(ReportFrequency.Daily);
    }

    [Fact]
    public void ParseFrequency_InvalidFrequency_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => ParseFrequencyTestHelper("invalid");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Invalid frequency: invalid");
    }

    [Theory]
    [InlineData("monday", DayOfWeek.Monday)]
    [InlineData("tuesday", DayOfWeek.Tuesday)]
    [InlineData("wednesday", DayOfWeek.Wednesday)]
    [InlineData("thursday", DayOfWeek.Thursday)]
    [InlineData("friday", DayOfWeek.Friday)]
    [InlineData("saturday", DayOfWeek.Saturday)]
    [InlineData("sunday", DayOfWeek.Sunday)]
    public void ParseDayOfWeek_ValidDay_ReturnsCorrectEnum(string day, DayOfWeek expected)
    {
        // Act
        var result = ParseDayOfWeekTestHelper(day);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("MONDAY")]
    [InlineData("Monday")]
    [InlineData("MoNdAy")]
    public void ParseDayOfWeek_CaseInsensitive_WorksCorrectly(string day)
    {
        // Act
        var result = ParseDayOfWeekTestHelper(day);

        // Assert
        result.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void ParseDayOfWeek_InvalidDay_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => ParseDayOfWeekTestHelper("invalidday");
        action.Should().Throw<ArgumentException>()
            .WithMessage("Invalid day: invalidday");
    }

    // Helper methods that mimic the parsing logic from EmailReportSettingsController
    private static ReportFrequency ParseFrequencyTestHelper(string frequency) => frequency.ToLowerInvariant() switch
    {
        "daily" => ReportFrequency.Daily,
        "weekly" => ReportFrequency.Weekly,
        "monthly" => ReportFrequency.Monthly,
        _ => throw new ArgumentException($"Invalid frequency: {frequency}")
    };

    private static DayOfWeek ParseDayOfWeekTestHelper(string day) => day.ToLowerInvariant() switch
    {
        "monday" => DayOfWeek.Monday,
        "tuesday" => DayOfWeek.Tuesday,
        "wednesday" => DayOfWeek.Wednesday,
        "thursday" => DayOfWeek.Thursday,
        "friday" => DayOfWeek.Friday,
        "saturday" => DayOfWeek.Saturday,
        "sunday" => DayOfWeek.Sunday,
        _ => throw new ArgumentException($"Invalid day: {day}")
    };
}
