using FluentAssertions;
using ProjectCallisto.Application.Validation;

namespace ProjectCallisto.Tests.Application.Validation;

public class TimezoneValidatorTests
{
    [Theory]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Singapore")]
    [InlineData("Australia/Sydney")]
    [InlineData("Pacific/Auckland")]
    [InlineData("Africa/Johannesburg")]
    public void IsValidTimezone_CommonTimezones_ReturnsTrue(string timezone)
    {
        // Act
        var result = TimezoneValidator.IsValidTimezone(timezone);

        // Assert
        result.Should().BeTrue($"{timezone} should be a valid timezone");
    }

    [Theory]
    [InlineData("  America/New_York  ")] // Whitespace trimming
    [InlineData("asia/singapore")] // Case insensitive
    [InlineData("EUROPE/LONDON")] // Case insensitive
    public void IsValidTimezone_TrimsAndIgnoresCase(string timezone)
    {
        // Act
        var result = TimezoneValidator.IsValidTimezone(timezone);

        // Assert
        result.Should().BeTrue($"{timezone} should be valid after trimming/case normalization");
    }

    [Theory]
    [InlineData("America/New_York\nAmerica/Los_Angeles")] // Newline injection
    [InlineData("Asia/Singapore\rAsia/Tokyo")] // Carriage return injection
    [InlineData("Europe/London\0")] // Null byte injection
    [InlineData("UTC\r\nEurope/Paris")] // CRLF injection
    public void IsValidTimezone_InjectionAttempts_ReturnsFalse(string maliciousTimezone)
    {
        // Act
        var result = TimezoneValidator.IsValidTimezone(maliciousTimezone);

        // Assert
        result.Should().BeFalse($"{maliciousTimezone} should be rejected (injection attempt)");
    }

    [Theory]
    [InlineData("Invalid/Timezone")]
    [InlineData("NotATimezone")]
    [InlineData("America/FakeCity")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IsValidTimezone_InvalidTimezones_ReturnsFalse(string? invalidTimezone)
    {
        // Act
        var result = TimezoneValidator.IsValidTimezone(invalidTimezone);

        // Assert
        result.Should().BeFalse($"{invalidTimezone ?? "(null)"} should be invalid");
    }

    [Fact]
    public void IsValidTimezone_TimezoneTooLong_ReturnsFalse()
    {
        // Arrange - Max length is 100 characters
        var longTimezone = new string('a', 101);

        // Act
        var result = TimezoneValidator.IsValidTimezone(longTimezone);

        // Assert
        result.Should().BeFalse("timezones longer than 100 characters should be rejected");
    }

    [Fact]
    public void IsValidTimezone_TimezoneWithControlCharacters_ReturnsFalse()
    {
        // Arrange - ASCII control character (tab)
        var timezoneWithTab = "America/New\t_York";

        // Act
        var result = TimezoneValidator.IsValidTimezone(timezoneWithTab);

        // Assert
        result.Should().BeFalse("timezones with control characters should be rejected");
    }

    [Fact]
    public void ValidateOrThrow_ValidTimezone_DoesNotThrow()
    {
        // Arrange
        var validTimezone = "America/New_York";

        // Act
        var act = () => TimezoneValidator.ValidateOrThrow(validTimezone);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOrThrow_InvalidTimezone_ThrowsArgumentException()
    {
        // Arrange
        var invalidTimezone = "Invalid/Timezone";

        // Act
        var act = () => TimezoneValidator.ValidateOrThrow(invalidTimezone, "testTimezone");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid or unsupported timezone*")
            .And.ParamName.Should().Be("testTimezone");
    }

    [Fact]
    public void ValidateOrThrow_NullTimezone_ThrowsArgumentException()
    {
        // Arrange
        string? nullTimezone = null;

        // Act
        var act = () => TimezoneValidator.ValidateOrThrow(nullTimezone);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid or unsupported timezone*");
    }

    [Fact]
    public void GetAllowedTimezones_ReturnsReadOnlyCollection()
    {
        // Act
        var allowedTimezones = TimezoneValidator.GetAllowedTimezones();

        // Assert
        allowedTimezones.Should().NotBeEmpty();
        allowedTimezones.Should().Contain("UTC");
        allowedTimezones.Should().Contain("America/New_York");
        allowedTimezones.Should().Contain("Europe/London");
        allowedTimezones.Should().Contain("Asia/Singapore");
        allowedTimezones.Should().Contain("Australia/Sydney");
    }

    [Theory]
    [InlineData("America/New_York<script>alert('xss')</script>")] // XSS attempt
    [InlineData("UTC; DROP TABLE organisations;--")] // SQL injection attempt
    [InlineData("../../etc/passwd")] // Path traversal attempt
    public void IsValidTimezone_MaliciousPayloads_ReturnsFalse(string maliciousTimezone)
    {
        // Act
        var result = TimezoneValidator.IsValidTimezone(maliciousTimezone);

        // Assert
        result.Should().BeFalse($"{maliciousTimezone} should be rejected (malicious payload)");
    }

    [Fact]
    public void IsValidTimezone_AllWhitelistedTimezones_AreValid()
    {
        // Arrange
        var allowedTimezones = TimezoneValidator.GetAllowedTimezones();

        // Act & Assert
        foreach (var timezone in allowedTimezones)
        {
            var result = TimezoneValidator.IsValidTimezone(timezone);
            result.Should().BeTrue($"{timezone} is whitelisted but validation failed");
        }
    }
}
