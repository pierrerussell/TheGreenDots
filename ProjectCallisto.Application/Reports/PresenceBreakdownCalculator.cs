using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Application.Reports;

public class PresenceBreakdownCalculator : IPresenceBreakdownCalculator
{
    private const double OFFLINE_GAP_THRESHOLD_HOURS = 1.5;

    public TimeBreakdown Calculate(
        List<PresenceHistory> records,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        // Edge case: no records
        if (records == null || records.Count == 0)
        {
            return new TimeBreakdown
            {
                TotalHours = 0,
                AvailableHours = 0,
                BusyHours = 0,
                AwayHours = 0,
                DoNotDisturbHours = 0,
                OfflineHours = 0,
                AvailablePercent = 0,
                BusyPercent = 0,
                AwayPercent = 0,
                DoNotDisturbPercent = 0,
                OfflinePercent = 0
            };
        }

        // Sort records by RecordedAt to ensure chronological order
        var sortedRecords = records.OrderBy(r => r.RecordedAt).ToList();

        // Convert snapshots to time segments
        var segments = ConvertSnapshotsToSegments(sortedRecords, periodStart, periodEnd);

        // Accumulate hours by status
        var statusHours = new Dictionary<string, double>
        {
            { "Available", 0 },
            { "Busy", 0 },
            { "Away", 0 },
            { "DoNotDisturb", 0 },
            { "Offline", 0 }
        };

        foreach (var segment in segments)
        {
            var normalizedStatus = NormalizeStatus(segment.Status);
            if (statusHours.ContainsKey(normalizedStatus))
            {
                statusHours[normalizedStatus] += segment.DurationHours;
            }
        }

        var totalHours = statusHours.Values.Sum();

        // Calculate percentages
        int CalculatePercent(double hours) =>
            totalHours > 0 ? (int)Math.Round((hours / totalHours) * 100) : 0;

        return new TimeBreakdown
        {
            TotalHours = totalHours,
            AvailableHours = statusHours["Available"],
            BusyHours = statusHours["Busy"],
            AwayHours = statusHours["Away"],
            DoNotDisturbHours = statusHours["DoNotDisturb"],
            OfflineHours = statusHours["Offline"],
            AvailablePercent = CalculatePercent(statusHours["Available"]),
            BusyPercent = CalculatePercent(statusHours["Busy"]),
            AwayPercent = CalculatePercent(statusHours["Away"]),
            DoNotDisturbPercent = CalculatePercent(statusHours["DoNotDisturb"]),
            OfflinePercent = CalculatePercent(statusHours["Offline"])
        };
    }

    private List<TimeSegment> ConvertSnapshotsToSegments(
        List<PresenceHistory> sortedRecords,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        var segments = new List<TimeSegment>();

        // Handle gap before first record
        if (sortedRecords[0].RecordedAt > periodStart)
        {
            var gapDuration = (sortedRecords[0].RecordedAt - periodStart).TotalHours;
            if (gapDuration > 0)
            {
                segments.Add(new TimeSegment
                {
                    StartTime = periodStart,
                    EndTime = sortedRecords[0].RecordedAt,
                    Status = "Offline",
                    DurationHours = gapDuration
                });
            }
        }

        // Process each record
        for (int i = 0; i < sortedRecords.Count; i++)
        {
            var current = sortedRecords[i];
            var nextRecordTime = (i < sortedRecords.Count - 1)
                ? sortedRecords[i + 1].RecordedAt
                : periodEnd;

            var duration = (nextRecordTime - current.RecordedAt).TotalHours;

            // Check for offline gap
            if (duration > OFFLINE_GAP_THRESHOLD_HOURS && i < sortedRecords.Count - 1)
            {
                // Split into: current status segment + offline segment

                // Add segment for current status (up to threshold)
                var currentSegmentEnd = current.RecordedAt.AddHours(OFFLINE_GAP_THRESHOLD_HOURS);
                segments.Add(new TimeSegment
                {
                    StartTime = current.RecordedAt,
                    EndTime = currentSegmentEnd,
                    Status = current.Availability,
                    DurationHours = OFFLINE_GAP_THRESHOLD_HOURS
                });

                // Add offline segment for the gap
                var offlineStart = currentSegmentEnd;
                var offlineEnd = nextRecordTime;
                var offlineDuration = (offlineEnd - offlineStart).TotalHours;

                if (offlineDuration > 0)
                {
                    segments.Add(new TimeSegment
                    {
                        StartTime = offlineStart,
                        EndTime = offlineEnd,
                        Status = "Offline",
                        DurationHours = offlineDuration
                    });
                }
            }
            else
            {
                // Normal segment - use current status for the entire duration
                if (duration > 0)
                {
                    segments.Add(new TimeSegment
                    {
                        StartTime = current.RecordedAt,
                        EndTime = nextRecordTime,
                        Status = current.Availability,
                        DurationHours = duration
                    });
                }
            }
        }

        return segments;
    }

    public TimeBreakdown CalculateWithoutFillingGaps(List<PresenceHistory> records)
    {
        // Edge case: no records
        if (records == null || records.Count == 0)
        {
            return new TimeBreakdown
            {
                TotalHours = 0,
                AvailableHours = 0,
                BusyHours = 0,
                AwayHours = 0,
                DoNotDisturbHours = 0,
                OfflineHours = 0,
                AvailablePercent = 0,
                BusyPercent = 0,
                AwayPercent = 0,
                DoNotDisturbPercent = 0,
                OfflinePercent = 0
            };
        }

        // Sort records by RecordedAt
        var sortedRecords = records.OrderBy(r => r.RecordedAt).ToList();

        // Accumulate hours by status - only count actual record durations up to threshold
        var statusHours = new Dictionary<string, double>
        {
            { "Available", 0 },
            { "Busy", 0 },
            { "Away", 0 },
            { "DoNotDisturb", 0 },
            { "Offline", 0 }
        };

        for (int i = 0; i < sortedRecords.Count; i++)
        {
            var current = sortedRecords[i];
            var nextRecordTime = (i < sortedRecords.Count - 1)
                ? sortedRecords[i + 1].RecordedAt
                : current.RecordedAt.AddHours(OFFLINE_GAP_THRESHOLD_HOURS);

            var duration = (nextRecordTime - current.RecordedAt).TotalHours;

            // Cap at threshold to avoid counting long gaps
            var cappedDuration = Math.Min(duration, OFFLINE_GAP_THRESHOLD_HOURS);

            var normalizedStatus = NormalizeStatus(current.Availability);
            if (statusHours.ContainsKey(normalizedStatus))
            {
                statusHours[normalizedStatus] += cappedDuration;
            }
        }

        var totalHours = statusHours.Values.Sum();

        // Calculate percentages
        int CalculatePercent(double hours) =>
            totalHours > 0 ? (int)Math.Round((hours / totalHours) * 100) : 0;

        return new TimeBreakdown
        {
            TotalHours = totalHours,
            AvailableHours = statusHours["Available"],
            BusyHours = statusHours["Busy"],
            AwayHours = statusHours["Away"],
            DoNotDisturbHours = statusHours["DoNotDisturb"],
            OfflineHours = statusHours["Offline"],
            AvailablePercent = CalculatePercent(statusHours["Available"]),
            BusyPercent = CalculatePercent(statusHours["Busy"]),
            AwayPercent = CalculatePercent(statusHours["Away"]),
            DoNotDisturbPercent = CalculatePercent(statusHours["DoNotDisturb"]),
            OfflinePercent = CalculatePercent(statusHours["Offline"])
        };
    }

    /// <summary>
    /// Calculates time breakdown for records that overlap with working hours,
    /// clipping each segment to only count the portion within the working hour windows.
    /// </summary>
    public TimeBreakdown CalculateForWorkingHours(
        List<PresenceHistory> allRecords,
        Domain.Organisations.WorkingHours workingHours,
        string timezone,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        // Edge case: no records
        if (allRecords == null || allRecords.Count == 0 || workingHours == null)
        {
            return new TimeBreakdown
            {
                TotalHours = 0,
                AvailableHours = 0,
                BusyHours = 0,
                AwayHours = 0,
                DoNotDisturbHours = 0,
                OfflineHours = 0,
                AvailablePercent = 0,
                BusyPercent = 0,
                AwayPercent = 0,
                DoNotDisturbPercent = 0,
                OfflinePercent = 0
            };
        }

        var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var sortedRecords = allRecords.OrderBy(r => r.RecordedAt).ToList();

        var statusHours = new Dictionary<string, double>
        {
            { "Available", 0 },
            { "Busy", 0 },
            { "Away", 0 },
            { "DoNotDisturb", 0 },
            { "Offline", 0 }
        };

        // Process each record
        for (int i = 0; i < sortedRecords.Count; i++)
        {
            var current = sortedRecords[i];
            var nextRecordTime = (i < sortedRecords.Count - 1)
                ? sortedRecords[i + 1].RecordedAt
                : current.RecordedAt.AddHours(OFFLINE_GAP_THRESHOLD_HOURS);

            // Clip segment to period bounds
            var segmentStart = current.RecordedAt < periodStart ? periodStart : current.RecordedAt;
            var segmentEnd = nextRecordTime > periodEnd ? periodEnd : nextRecordTime;

            if (segmentEnd <= segmentStart)
                continue;

            // Calculate overlap with working hour windows
            var workingTimeInSegment = CalculateWorkingTimeInSegment(
                segmentStart,
                segmentEnd,
                workingHours,
                tzInfo);

            if (workingTimeInSegment > 0)
            {
                var normalizedStatus = NormalizeStatus(current.Availability);
                if (statusHours.ContainsKey(normalizedStatus))
                {
                    statusHours[normalizedStatus] += workingTimeInSegment;
                }
            }
        }

        var totalHours = statusHours.Values.Sum();

        // Calculate percentages
        int CalculatePercent(double hours) =>
            totalHours > 0 ? (int)Math.Round((hours / totalHours) * 100) : 0;

        return new TimeBreakdown
        {
            TotalHours = totalHours,
            AvailableHours = statusHours["Available"],
            BusyHours = statusHours["Busy"],
            AwayHours = statusHours["Away"],
            DoNotDisturbHours = statusHours["DoNotDisturb"],
            OfflineHours = statusHours["Offline"],
            AvailablePercent = CalculatePercent(statusHours["Available"]),
            BusyPercent = CalculatePercent(statusHours["Busy"]),
            AwayPercent = CalculatePercent(statusHours["Away"]),
            DoNotDisturbPercent = CalculatePercent(statusHours["DoNotDisturb"]),
            OfflinePercent = CalculatePercent(statusHours["Offline"])
        };
    }

    /// <summary>
    /// Calculates how many hours in a time segment fall within working hours.
    /// Handles segments that span multiple days and working hour windows.
    /// </summary>
    private double CalculateWorkingTimeInSegment(
        DateTimeOffset segmentStart,
        DateTimeOffset segmentEnd,
        Domain.Organisations.WorkingHours workingHours,
        TimeZoneInfo tzInfo)
    {
        var totalWorkingMinutes = 0.0;
        var current = segmentStart;

        // Iterate through each day in the segment
        while (current < segmentEnd)
        {
            var currentLocal = TimeZoneInfo.ConvertTime(current, tzInfo);
            var currentDate = currentLocal.Date;

            // Check if this day is a working day
            var dayFlag = currentLocal.DayOfWeek switch
            {
                DayOfWeek.Monday => Domain.Organisations.WorkingDaysFlags.Monday,
                DayOfWeek.Tuesday => Domain.Organisations.WorkingDaysFlags.Tuesday,
                DayOfWeek.Wednesday => Domain.Organisations.WorkingDaysFlags.Wednesday,
                DayOfWeek.Thursday => Domain.Organisations.WorkingDaysFlags.Thursday,
                DayOfWeek.Friday => Domain.Organisations.WorkingDaysFlags.Friday,
                DayOfWeek.Saturday => Domain.Organisations.WorkingDaysFlags.Saturday,
                DayOfWeek.Sunday => Domain.Organisations.WorkingDaysFlags.Sunday,
                _ => Domain.Organisations.WorkingDaysFlags.None
            };

            if (workingHours.WorkingDays.HasFlag(dayFlag))
            {
                // Calculate working hours window for this day in UTC
                var workStartLocal = new DateTime(
                    currentDate.Year, currentDate.Month, currentDate.Day,
                    workingHours.StartTime.Hour, workingHours.StartTime.Minute, 0);
                var workEndLocal = new DateTime(
                    currentDate.Year, currentDate.Month, currentDate.Day,
                    workingHours.EndTime.Hour, workingHours.EndTime.Minute, 0);

                var workStartUtc = TimeZoneInfo.ConvertTimeToUtc(workStartLocal, tzInfo);
                var workEndUtc = TimeZoneInfo.ConvertTimeToUtc(workEndLocal, tzInfo);

                // Find overlap between segment and working hours window
                var overlapStart = current > workStartUtc ? current : workStartUtc;
                var overlapEnd = segmentEnd < workEndUtc ? segmentEnd : workEndUtc;

                if (overlapEnd > overlapStart)
                {
                    totalWorkingMinutes += (overlapEnd - overlapStart).TotalMinutes;
                }
            }

            // Move to start of next day
            var nextDayLocal = currentDate.AddDays(1);
            current = TimeZoneInfo.ConvertTimeToUtc(nextDayLocal, tzInfo);
        }

        return totalWorkingMinutes / 60.0; // Convert to hours
    }

    private string NormalizeStatus(string availability)
    {
        // Map Microsoft Graph availability values to our breakdown categories
        return availability switch
        {
            "Available" => "Available",
            "Busy" => "Busy",
            "InACall" => "Busy",
            "InAConferenceCall" => "Busy",
            "InAMeeting" => "Busy",
            "Presenting" => "Busy",
            "Away" => "Away",
            "BeRightBack" => "Away",
            "Inactive" => "Away",
            "OffWork" => "Away",
            "OutOfOffice" => "Away",
            "DoNotDisturb" => "DoNotDisturb",
            "UrgentInterruptionsOnly" => "DoNotDisturb",
            "Offline" => "Offline",
            "PresenceUnknown" => "Offline",
            _ => "Offline"
        };
    }

    private class TimeSegment
    {
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public double DurationHours { get; set; }
    }
}
