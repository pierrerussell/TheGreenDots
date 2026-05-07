namespace ProjectCallisto.Application.Validation;

/// <summary>
/// Provides timezone validation with a whitelist of allowed IANA timezones.
/// </summary>
public static class TimezoneValidator
{
    /// <summary>
    /// Whitelist of allowed IANA timezone identifiers.
    /// Covers major cities across all continents for legitimate business use.
    /// </summary>
    private static readonly HashSet<string> AllowedTimezones = new(StringComparer.OrdinalIgnoreCase)
    {
        // UTC
        "UTC",
        "Etc/UTC",

        // Americas
        "America/New_York",       // US Eastern
        "America/Chicago",        // US Central
        "America/Denver",         // US Mountain
        "America/Los_Angeles",    // US Pacific
        "America/Anchorage",      // US Alaska
        "America/Phoenix",        // US Arizona (no DST)
        "America/Toronto",        // Canada Eastern
        "America/Vancouver",      // Canada Pacific
        "America/Mexico_City",    // Mexico
        "America/Sao_Paulo",      // Brazil
        "America/Buenos_Aires",   // Argentina
        "America/Santiago",       // Chile
        "America/Bogota",         // Colombia
        "America/Lima",           // Peru

        // Europe
        "Europe/London",          // UK
        "Europe/Dublin",          // Ireland
        "Europe/Paris",           // France
        "Europe/Berlin",          // Germany
        "Europe/Madrid",          // Spain
        "Europe/Rome",            // Italy
        "Europe/Amsterdam",       // Netherlands
        "Europe/Brussels",        // Belgium
        "Europe/Vienna",          // Austria
        "Europe/Zurich",          // Switzerland
        "Europe/Stockholm",       // Sweden
        "Europe/Oslo",            // Norway
        "Europe/Copenhagen",      // Denmark
        "Europe/Helsinki",        // Finland
        "Europe/Warsaw",          // Poland
        "Europe/Prague",          // Czech Republic
        "Europe/Budapest",        // Hungary
        "Europe/Bucharest",       // Romania
        "Europe/Athens",          // Greece
        "Europe/Istanbul",        // Turkey
        "Europe/Moscow",          // Russia
        "Europe/Lisbon",          // Portugal

        // Asia
        "Asia/Dubai",             // UAE
        "Asia/Riyadh",            // Saudi Arabia
        "Asia/Kolkata",           // India
        "Asia/Karachi",           // Pakistan
        "Asia/Dhaka",             // Bangladesh
        "Asia/Bangkok",           // Thailand
        "Asia/Singapore",         // Singapore
        "Asia/Kuala_Lumpur",      // Malaysia
        "Asia/Jakarta",           // Indonesia
        "Asia/Manila",            // Philippines
        "Asia/Hong_Kong",         // Hong Kong
        "Asia/Shanghai",          // China
        "Asia/Taipei",            // Taiwan
        "Asia/Seoul",             // South Korea
        "Asia/Tokyo",             // Japan
        "Asia/Ho_Chi_Minh",       // Vietnam
        "Asia/Jerusalem",         // Israel
        "Asia/Tbilisi",           // Georgia

        // Africa
        "Africa/Cairo",           // Egypt
        "Africa/Johannesburg",    // South Africa
        "Africa/Lagos",           // Nigeria
        "Africa/Nairobi",         // Kenya
        "Africa/Casablanca",      // Morocco
        "Africa/Algiers",         // Algeria
        "Africa/Tunis",           // Tunisia

        // Oceania
        "Pacific/Auckland",       // New Zealand
        "Pacific/Fiji",           // Fiji
        "Australia/Sydney",       // Australia Eastern
        "Australia/Melbourne",    // Australia Eastern (Victoria)
        "Australia/Brisbane",     // Australia Eastern (Queensland, no DST)
        "Australia/Perth",        // Australia Western
        "Australia/Adelaide",     // Australia Central
        "Pacific/Honolulu",       // Hawaii
        "Pacific/Guam",           // Guam
    };

    /// <summary>
    /// Validates a timezone identifier against the whitelist.
    /// </summary>
    /// <param name="timezone">The IANA timezone identifier to validate</param>
    /// <returns>True if valid and whitelisted, false otherwise</returns>
    public static bool IsValidTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return false;

        timezone = timezone.Trim();

        // Enforce max length (IANA timezones are typically < 40 chars)
        if (timezone.Length > 100)
            return false;

        // Reject timezones with suspicious characters (injection attempts)
        if (timezone.Contains('\n') || timezone.Contains('\r') || timezone.Contains('\0'))
            return false;

        // Check control characters
        if (timezone.Any(c => char.IsControl(c)))
            return false;

        // Must be in whitelist
        if (!AllowedTimezones.Contains(timezone))
            return false;

        // Final verification: Ensure TimeZoneInfo can find it on this system
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            // Valid IANA timezone but not available on this system
            // This shouldn't happen for common timezones, log for investigation
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a timezone and throws an exception if invalid.
    /// </summary>
    /// <param name="timezone">The timezone identifier to validate</param>
    /// <param name="paramName">The parameter name for exception message</param>
    /// <exception cref="ArgumentException">Thrown when timezone is invalid or not whitelisted</exception>
    public static void ValidateOrThrow(string? timezone, string paramName = "timezone")
    {
        if (!IsValidTimezone(timezone))
        {
            throw new ArgumentException(
                $"Invalid or unsupported timezone: {timezone}. Please use a valid IANA timezone identifier from the supported list.",
                paramName);
        }
    }

    /// <summary>
    /// Gets all allowed timezone identifiers.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllowedTimezones()
    {
        return AllowedTimezones.ToList().AsReadOnly();
    }
}
