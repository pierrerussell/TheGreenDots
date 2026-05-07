using System.Net.Mail;

namespace ProjectCallisto.Application.Validation;

/// <summary>
/// Provides email address validation to prevent email header injection attacks.
/// </summary>
public static class EmailValidator
{
    /// <summary>
    /// Validates an email address using strict rules to prevent header injection.
    /// </summary>
    /// <param name="email">The email address to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim();

        // Reject emails with newlines or control characters (header injection)
        if (email.Contains('\n') || email.Contains('\r') || email.Contains('\0'))
            return false;

        // Reject emails with other control characters (ASCII 0-31, except space)
        if (email.Any(c => char.IsControl(c)))
            return false;

        // Enforce reasonable length (RFC 5321 says 254 max)
        if (email.Length > 254)
            return false;

        // Use built-in .NET validation (stricter than just checking for @)
        try
        {
            var mailAddress = new MailAddress(email);

            // Ensure MailAddress didn't "fix" the input (should match exactly)
            return mailAddress.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates an email address and throws an exception if invalid.
    /// </summary>
    /// <param name="email">The email address to validate</param>
    /// <param name="paramName">The parameter name for exception message</param>
    /// <exception cref="ArgumentException">Thrown when email is invalid</exception>
    public static void ValidateOrThrow(string? email, string paramName = "email")
    {
        if (!IsValidEmail(email))
        {
            throw new ArgumentException(
                $"Invalid email address. Email addresses must be properly formatted and cannot contain control characters or newlines.",
                paramName);
        }
    }
}
