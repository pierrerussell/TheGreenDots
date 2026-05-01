namespace ProjectCallisto.Domain.Organisations;

public class EmailReportSettings
{
    private EmailReportSettings() { } // EF Core

    public EmailReportSettings(Guid organisationId)
    {
        Id = Guid.NewGuid();
        OrganisationId = organisationId;

        // Defaults
        IsEnabled = false; // Opt-in
        Frequency = ReportFrequency.Weekly;
        DayOfWeek = System.DayOfWeek.Monday; // Default for weekly reports
        DayOfMonth = 1; // Default for monthly reports
        TimeOfDay = new TimeOnly(9, 0); // 9 AM in org's timezone
        Recipients = new List<EmailRecipient>();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }

    public bool IsEnabled { get; set; }
    public ReportFrequency Frequency { get; set; }

    // Frequency-specific settings
    public DayOfWeek? DayOfWeek { get; set; } // For Weekly: Monday-Sunday
    public int? DayOfMonth { get; set; } // For Monthly: 1-28 (safe range)
    public TimeOnly TimeOfDay { get; set; } // Time to send in org's timezone

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastSentAt { get; set; } // Track last send time for idempotency

    // Navigation
    public Organisation Organisation { get; set; } = null!;
    public List<EmailRecipient> Recipients { get; set; } = new();
}

public enum ReportFrequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

public class EmailRecipient
{
    private EmailRecipient() { } // EF Core

    public EmailRecipient(Guid emailReportSettingsId, string email, string? name = null)
    {
        Id = Guid.NewGuid();
        EmailReportSettingsId = emailReportSettingsId;
        Email = email;
        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; set; }
    public Guid EmailReportSettingsId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; } // Optional display name

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public EmailReportSettings EmailReportSettings { get; set; } = null!;
}
