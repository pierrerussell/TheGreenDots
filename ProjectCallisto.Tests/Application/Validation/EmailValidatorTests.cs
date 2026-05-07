using FluentAssertions;
using ProjectCallisto.Application.Validation;

namespace ProjectCallisto.Tests.Application.Validation;

public class EmailValidatorTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.user@example.com")]
    [InlineData("test+tag@example.co.uk")]
    [InlineData("user123@subdomain.example.com")]
    [InlineData("a@b.c")] // Technically valid, though unusual
    public void IsValidEmail_ValidEmails_ReturnsTrue(string email)
    {
        // Act
        var result = EmailValidator.IsValidEmail(email);

        // Assert
        result.Should().BeTrue($"{email} should be a valid email");
    }

    [Theory]
    [InlineData("user@example.com\nBcc: attacker@evil.com")] // Header injection with newline
    [InlineData("user@example.com\rBcc: attacker@evil.com")] // Header injection with carriage return
    [InlineData("user@example.com\r\nBcc: attacker@evil.com")] // Header injection with CRLF
    [InlineData("user@example.com\0")] // Null byte injection
    public void IsValidEmail_HeaderInjectionAttempts_ReturnsFalse(string maliciousEmail)
    {
        // Act
        var result = EmailValidator.IsValidEmail(maliciousEmail);

        // Assert
        result.Should().BeFalse($"{maliciousEmail} should be rejected (header injection attempt)");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IsValidEmail_InvalidFormats_ReturnsFalse(string? invalidEmail)
    {
        // Act
        var result = EmailValidator.IsValidEmail(invalidEmail);

        // Assert
        result.Should().BeFalse($"{invalidEmail ?? "(null)"} should be invalid");
    }

    [Fact]
    public void IsValidEmail_EmailTooLong_ReturnsFalse()
    {
        // Arrange - RFC 5321 says max 254 characters
        var longEmail = new string('a', 250) + "@example.com"; // 262 chars total

        // Act
        var result = EmailValidator.IsValidEmail(longEmail);

        // Assert
        result.Should().BeFalse("emails longer than 254 characters should be rejected");
    }

    [Fact]
    public void IsValidEmail_EmailWithControlCharacters_ReturnsFalse()
    {
        // Arrange - ASCII control character (tab)
        var emailWithTab = "user\t@example.com";

        // Act
        var result = EmailValidator.IsValidEmail(emailWithTab);

        // Assert
        result.Should().BeFalse("emails with control characters should be rejected");
    }

    [Fact]
    public void IsValidEmail_TrimsWhitespace()
    {
        // Arrange
        var emailWithSpaces = "  user@example.com  ";

        // Act
        var result = EmailValidator.IsValidEmail(emailWithSpaces);

        // Assert
        result.Should().BeTrue("leading/trailing whitespace should be trimmed");
    }

    [Fact]
    public void ValidateOrThrow_ValidEmail_DoesNotThrow()
    {
        // Arrange
        var validEmail = "user@example.com";

        // Act
        var act = () => EmailValidator.ValidateOrThrow(validEmail);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOrThrow_InvalidEmail_ThrowsArgumentException()
    {
        // Arrange
        var invalidEmail = "user@example.com\nBcc: attacker@evil.com";

        // Act
        var act = () => EmailValidator.ValidateOrThrow(invalidEmail, "testEmail");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid email address*")
            .And.ParamName.Should().Be("testEmail");
    }

    [Fact]
    public void ValidateOrThrow_NullEmail_ThrowsArgumentException()
    {
        // Arrange
        string? nullEmail = null;

        // Act
        var act = () => EmailValidator.ValidateOrThrow(nullEmail);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid email address*");
    }

    [Theory]
    [InlineData("admin@example.com<script>alert('xss')</script>")] // XSS attempt
    [InlineData("user@example.com; DROP TABLE users;--")] // SQL injection attempt (shouldn't parse as valid)
    public void IsValidEmail_MaliciousPayloads_ReturnsFalse(string maliciousEmail)
    {
        // Act
        var result = EmailValidator.IsValidEmail(maliciousEmail);

        // Assert
        result.Should().BeFalse($"{maliciousEmail} should be rejected (malicious payload)");
    }
}
