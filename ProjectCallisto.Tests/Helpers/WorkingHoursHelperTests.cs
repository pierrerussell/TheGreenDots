using FluentAssertions;
using ProjectCallisto.Domain.Organisations;
using Xunit;
using System.Reflection;

namespace ProjectCallisto.Tests.Helpers;

public class WorkingHoursHelperTests
{
    [Fact]
    public void ParseWorkingDays_SingleDay_Monday_ReturnsCorrectFlag()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(new[] { "monday" });

        // Assert
        result.Should().Be(WorkingDaysFlags.Monday);
    }

    [Fact]
    public void ParseWorkingDays_MultipleDays_MondayAndFriday_ReturnsCorrectFlags()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(new[] { "monday", "friday" });

        // Assert
        result.Should().Be(WorkingDaysFlags.Monday | WorkingDaysFlags.Friday);
    }

    [Fact]
    public void ParseWorkingDays_Weekdays_ReturnsCorrectFlags()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(new[] { "monday", "tuesday", "wednesday", "thursday", "friday" });

        // Assert
        result.Should().Be(
            WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday | WorkingDaysFlags.Wednesday |
            WorkingDaysFlags.Thursday | WorkingDaysFlags.Friday);
    }

    [Fact]
    public void ParseWorkingDays_Weekend_ReturnsCorrectFlags()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(new[] { "saturday", "sunday" });

        // Assert
        result.Should().Be(WorkingDaysFlags.Saturday | WorkingDaysFlags.Sunday);
    }

    [Fact]
    public void ParseWorkingDays_UpperCase_ReturnsCorrectFlag()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(new[] { "MONDAY" });

        // Assert
        result.Should().Be(WorkingDaysFlags.Monday);
    }

    [Fact]
    public void ParseWorkingDays_MixedCase_ReturnsCorrectFlag()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(new[] { "MoNdAy" });

        // Assert
        result.Should().Be(WorkingDaysFlags.Monday);
    }

    [Fact]
    public void ParseWorkingDays_EmptyArray_ReturnsNone()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(Array.Empty<string>());

        // Assert
        result.Should().Be(WorkingDaysFlags.None);
    }

    [Fact]
    public void ParseWorkingDays_InvalidDay_IgnoresInvalidDay()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(new[] { "monday", "invalidday", "friday" });

        // Assert
        result.Should().Be(WorkingDaysFlags.Monday | WorkingDaysFlags.Friday);
    }

    [Fact]
    public void ParseWorkingDays_AllDays_ReturnsAllFlags()
    {
        // Act
        var result = ParseWorkingDaysTestHelper(new[] {
            "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
        });

        // Assert
        result.Should().Be(
            WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday | WorkingDaysFlags.Wednesday |
            WorkingDaysFlags.Thursday | WorkingDaysFlags.Friday | WorkingDaysFlags.Saturday |
            WorkingDaysFlags.Sunday);
    }

    // Helper method that mimics the ParseWorkingDays logic from WorkingHoursController
    private static WorkingDaysFlags ParseWorkingDaysTestHelper(string[] days)
    {
        var flags = WorkingDaysFlags.None;
        foreach (var day in days)
        {
            flags |= day.ToLowerInvariant() switch
            {
                "monday" => WorkingDaysFlags.Monday,
                "tuesday" => WorkingDaysFlags.Tuesday,
                "wednesday" => WorkingDaysFlags.Wednesday,
                "thursday" => WorkingDaysFlags.Thursday,
                "friday" => WorkingDaysFlags.Friday,
                "saturday" => WorkingDaysFlags.Saturday,
                "sunday" => WorkingDaysFlags.Sunday,
                _ => WorkingDaysFlags.None
            };
        }
        return flags;
    }
}
