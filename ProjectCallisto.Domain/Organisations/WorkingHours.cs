namespace ProjectCallisto.Domain.Organisations;

public class WorkingHours
{
    private WorkingHours() { } // EF Core

    public WorkingHours(Guid organisationId)
    {
        Id = Guid.NewGuid();
        OrganisationId = organisationId;

        // Defaults: 9 AM - 5 PM, Monday-Friday
        StartTime = new TimeOnly(9, 0);
        EndTime = new TimeOnly(17, 0);
        WorkingDays = WorkingDaysFlags.Monday | WorkingDaysFlags.Tuesday |
                      WorkingDaysFlags.Wednesday | WorkingDaysFlags.Thursday |
                      WorkingDaysFlags.Friday;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; set; }
    public Guid OrganisationId { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public WorkingDaysFlags WorkingDays { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Organisation Organisation { get; set; } = null!;

    /// <summary>
    /// Checks if a given timestamp falls within working hours in the organization's timezone
    /// </summary>
    public bool IsWithinWorkingHours(DateTimeOffset timestamp, string orgTimezone)
    {
        // Convert timestamp to org's timezone
        var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(orgTimezone);
        var localTime = TimeZoneInfo.ConvertTime(timestamp, tzInfo);

        // Check day of week
        var dayFlag = localTime.DayOfWeek switch
        {
            DayOfWeek.Monday => WorkingDaysFlags.Monday,
            DayOfWeek.Tuesday => WorkingDaysFlags.Tuesday,
            DayOfWeek.Wednesday => WorkingDaysFlags.Wednesday,
            DayOfWeek.Thursday => WorkingDaysFlags.Thursday,
            DayOfWeek.Friday => WorkingDaysFlags.Friday,
            DayOfWeek.Saturday => WorkingDaysFlags.Saturday,
            DayOfWeek.Sunday => WorkingDaysFlags.Sunday,
            _ => WorkingDaysFlags.None
        };

        if (!WorkingDays.HasFlag(dayFlag))
            return false;

        // Check time range
        var timeOfDay = TimeOnly.FromDateTime(localTime.DateTime);
        return timeOfDay >= StartTime && timeOfDay <= EndTime;
    }

    /// <summary>
    /// Calculates the total expected working hours per week based on configured working days and times
    /// </summary>
    public double GetExpectedHoursPerWeek()
    {
        // Calculate hours per day
        var hoursPerDay = (EndTime - StartTime).TotalHours;

        // Count working days
        var workingDayCount = 0;
        if (WorkingDays.HasFlag(WorkingDaysFlags.Monday)) workingDayCount++;
        if (WorkingDays.HasFlag(WorkingDaysFlags.Tuesday)) workingDayCount++;
        if (WorkingDays.HasFlag(WorkingDaysFlags.Wednesday)) workingDayCount++;
        if (WorkingDays.HasFlag(WorkingDaysFlags.Thursday)) workingDayCount++;
        if (WorkingDays.HasFlag(WorkingDaysFlags.Friday)) workingDayCount++;
        if (WorkingDays.HasFlag(WorkingDaysFlags.Saturday)) workingDayCount++;
        if (WorkingDays.HasFlag(WorkingDaysFlags.Sunday)) workingDayCount++;

        return hoursPerDay * workingDayCount;
    }

    /// <summary>
    /// Calculates the total expected working hours per day based on configured working times
    /// </summary>
    public double GetExpectedHoursPerDay()
    {
        return (EndTime - StartTime).TotalHours;
    }
}

[Flags]
public enum WorkingDaysFlags
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64
}
