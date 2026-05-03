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
                var currentSegmentEnd = current.RecordedAt.AddHours(OFFLINE_GAP_THRESHOLD_HOURS / 2);
                segments.Add(new TimeSegment
                {
                    StartTime = current.RecordedAt,
                    EndTime = currentSegmentEnd,
                    Status = current.Availability,
                    DurationHours = OFFLINE_GAP_THRESHOLD_HOURS / 2
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
