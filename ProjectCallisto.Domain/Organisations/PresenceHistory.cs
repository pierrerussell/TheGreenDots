namespace ProjectCallisto.Domain.Organisations;

public class PresenceHistory
{
    public Guid Id { get; set; }
    public Guid TenantMemberId { get; set; }
    // Microsoft Graph availability: Available, Away, BeRightBack, Busy, DoNotDisturb, Offline, PresenceUnknown
    public string Availability { get; set; } = string.Empty;
    // Microsoft Graph activity: Available, Away, BeRightBack, Busy, DoNotDisturb, InACall, InAConferenceCall, Inactive, InAMeeting, Offline, OffWork, OutOfOffice, PresenceUnknown, Presenting, UrgentInterruptionsOnly
    public string Activity { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}
