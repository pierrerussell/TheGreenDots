using System.Text;
using ProjectCallisto.Application.Reports.Models;

namespace ProjectCallisto.Application.Reports;

public class ReportEmailHtmlGenerator
{
    public string GenerateDailyReportHtml(
        string orgName,
        string reportDate,
        string reportPeriod,
        int trackedCount,
        List<EmployeePresenceBreakdown> employees,
        DateTime generatedAt,
        DateTimeOffset? periodStart = null,
        DateTimeOffset? periodEnd = null)
    {
        var html = new StringBuilder();

        html.Append($@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Daily Presence Report - {reportDate}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f5f7f6; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif;"">

    <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color: #f5f7f6;"">
        <tr>
            <td style=""padding: 40px 20px;"">

                <!-- Email Container -->
                <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""680"" style=""margin: 0 auto; background-color: #ffffff; box-shadow: 0 2px 8px rgba(0,0,0,0.06);"">

                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #0a3d28 0%, #0f5132 100%); padding: 28px 32px; border-bottom: 3px solid #ffc107;"">
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                                <tr>
                                    <td style=""width: 70%;"">
                                        <div style=""margin-bottom: 8px;"">
                                            <div style=""display: inline-block; width: 6px; height: 6px; background-color: #4ade80; border-radius: 50%; margin-right: 4px;""></div>
                                            <div style=""display: inline-block; width: 6px; height: 6px; background-color: #4ade80; border-radius: 50%; margin-right: 4px;""></div>
                                            <div style=""display: inline-block; width: 6px; height: 6px; background-color: #4ade80; border-radius: 50%;""></div>
                                        </div>
                                        <h1 style=""margin: 0; color: #ffffff; font-size: 24px; font-weight: 700; letter-spacing: -0.3px;"">
                                            Daily Timeline Report
                                        </h1>
                                        <p style=""margin: 4px 0 0 0; color: #a7f3d0; font-size: 13px;"">
                                            {orgName} · {reportDate}
                                        </p>
                                    </td>
                                    <td style=""width: 30%; text-align: right; vertical-align: bottom;"">
                                        <p style=""margin: 0; color: #ffffff; font-size: 11px; text-transform: uppercase; letter-spacing: 0.8px; opacity: 0.7;"">
                                            Tracked Members
                                        </p>
                                        <p style=""margin: 2px 0 0 0; color: #ffffff; font-size: 28px; font-family: 'Courier New', monospace; font-weight: 700;"">
                                            {trackedCount}
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Greeting -->
                    <tr>
                        <td style=""padding: 24px 32px 16px 32px;"">
                            <p style=""margin: 0; color: #1f2937; font-size: 14px; line-height: 1.5;"">
                                Hello,
                            </p>
                            <p style=""margin: 8px 0 0 0; color: #4b5563; font-size: 13px; line-height: 1.6;"">
                                Here's yesterday's presence activity ({reportPeriod}) shown as 24-hour timelines. Each bar represents one team member's full day from midnight to midnight.
                            </p>
                        </td>
                    </tr>

                    <!-- Legend -->
                    <tr>
                        <td style=""padding: 0 32px 16px 32px;"">
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color: #f9fafb; border: 1px solid #e5e7eb; border-radius: 4px; padding: 12px 16px;"">
                                <tr>
                                    <td>
                                        <p style=""margin: 0 0 6px 0; color: #6b7280; font-size: 10px; text-transform: uppercase; letter-spacing: 0.6px; font-weight: 600;"">
                                            Status Legend
                                        </p>
                                        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                                            <tr>
                                                <td style=""padding-right: 16px;"">
                                                    <div style=""display: inline-block; width: 12px; height: 12px; background-color: #22c55e; border-radius: 2px; margin-right: 6px; vertical-align: middle;""></div>
                                                    <span style=""color: #4b5563; font-size: 11px; vertical-align: middle;"">Available</span>
                                                </td>
                                                <td style=""padding-right: 16px;"">
                                                    <div style=""display: inline-block; width: 12px; height: 12px; background-color: #ef4444; border-radius: 2px; margin-right: 6px; vertical-align: middle;""></div>
                                                    <span style=""color: #4b5563; font-size: 11px; vertical-align: middle;"">Busy</span>
                                                </td>
                                                <td style=""padding-right: 16px;"">
                                                    <div style=""display: inline-block; width: 12px; height: 12px; background-color: #f59e0b; border-radius: 2px; margin-right: 6px; vertical-align: middle;""></div>
                                                    <span style=""color: #4b5563; font-size: 11px; vertical-align: middle;"">Away</span>
                                                </td>
                                                <td style=""padding-right: 16px;"">
                                                    <div style=""display: inline-block; width: 12px; height: 12px; background-color: #8b5cf6; border-radius: 2px; margin-right: 6px; vertical-align: middle;""></div>
                                                    <span style=""color: #4b5563; font-size: 11px; vertical-align: middle;"">Do Not Disturb</span>
                                                </td>
                                                <td>
                                                    <div style=""display: inline-block; width: 12px; height: 12px; background-color: #d1d5db; border-radius: 2px; margin-right: 6px; vertical-align: middle;""></div>
                                                    <span style=""color: #4b5563; font-size: 11px; vertical-align: middle;"">Offline</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Timeline Section Header -->
                    <tr>
                        <td style=""padding: 16px 32px 12px 32px;"">
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                                <tr>
                                    <td style=""width: 65%;"">
                                        <h2 style=""margin: 0; color: #111827; font-size: 15px; font-weight: 700; letter-spacing: -0.2px;"">
                                            Team Member Timelines
                                        </h2>
                                    </td>
                                    <td style=""width: 35%; text-align: right;"">
                                        <p style=""margin: 0; color: #6b7280; font-size: 10px; text-transform: uppercase; letter-spacing: 0.6px;"">
                                            Total Online Hours
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Time Axis Labels -->
                    <tr>
                        <td style=""padding: 0 32px 8px 32px;"">
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                                <tr>
                                    <td style=""width: 180px;""></td>
                                    <td>
                                        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                                            <tr>
                                                <td style=""width: 0%; text-align: left;"">
                                                    <span style=""color: #9ca3af; font-size: 9px; font-family: 'Courier New', monospace;"">00:00</span>
                                                </td>
                                                <td style=""width: 25%; text-align: center;"">
                                                    <span style=""color: #9ca3af; font-size: 9px; font-family: 'Courier New', monospace;"">06:00</span>
                                                </td>
                                                <td style=""width: 50%; text-align: center;"">
                                                    <span style=""color: #9ca3af; font-size: 9px; font-family: 'Courier New', monospace;"">12:00</span>
                                                </td>
                                                <td style=""width: 75%; text-align: center;"">
                                                    <span style=""color: #9ca3af; font-size: 9px; font-family: 'Courier New', monospace;"">18:00</span>
                                                </td>
                                                <td style=""width: 100%; text-align: right;"">
                                                    <span style=""color: #9ca3af; font-size: 9px; font-family: 'Courier New', monospace;"">23:59</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                    <td style=""width: 80px;""></td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Employee Rows -->
");

        // Generate employee timeline rows
        foreach (var employee in employees)
        {
            html.Append(GenerateDailyEmployeeRow(employee));
        }

        html.Append($@"
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f9fafb; padding: 20px 32px; border-top: 1px solid #e5e7eb;"">
                            <p style=""margin: 0 0 6px 0; color: #6b7280; font-size: 11px;"">
                                <strong style=""color: #111827;"">The Green Dots</strong> · Presence Analytics
                            </p>
                            <p style=""margin: 0; color: #9ca3af; font-size: 10px; line-height: 1.5;"">
                                Report generated on {generatedAt:MMMM d, yyyy} at {generatedAt:h:mm tt} UTC<br>
                                Showing data for: {reportPeriod}
                            </p>
                            <p style=""margin: 10px 0 0 0;"">
                                <a href=""https://thegreendots.app/settings/email-preferences"" style=""color: #0f5132; font-size: 10px; text-decoration: underline;"">Manage email preferences</a>
                            </p>
                        </td>
                    </tr>

                </table>

            </td>
        </tr>
    </table>

</body>
</html>");

        return html.ToString();
    }

    private string GenerateDailyEmployeeRow(EmployeePresenceBreakdown employee)
    {
        var breakdown = employee.FullWeekBreakdown; // For daily, we use the full period (24-hour timeline)
        var totalOnlineHours = breakdown.TotalHours - breakdown.OfflineHours;
        var hasInsights = employee.Insights.Any();

        var bgColor = hasInsights ? "#fffbeb" : "#fafbfc";
        var borderColor = hasInsights ? "#fbbf24" : "#e5e7eb";
        var borderWidth = hasInsights ? "2px" : "1px";

        var insightHtml = hasInsights
            ? $@"<p style=""margin: 4px 0 0 0; color: #d97706; font-size: 10px; font-weight: 600;"">⚠️ {string.Join(", ", employee.Insights.Select(i => i.Message))}</p>"
            : "";

        return $@"
                    <tr>
                        <td style=""padding: 0 32px 12px 32px;"">
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color: {bgColor}; border: {borderWidth} solid {borderColor}; border-radius: 4px; padding: 12px;"">
                                <tr>
                                    <td style=""width: 180px; vertical-align: top; padding-right: 12px;"">
                                        <p style=""margin: 0; color: #111827; font-size: 13px; font-weight: 600;"">{employee.DisplayName}</p>
                                        <p style=""margin: 2px 0 0 0; color: #6b7280; font-size: 11px;"">{employee.Email}</p>
                                        {insightHtml}
                                    </td>
                                    <td style=""vertical-align: middle; padding-right: 12px;"">
                                        <!-- 24-hour timeline bar -->
                                        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""height: 18px; border-radius: 3px; overflow: hidden; border: 1px solid {borderColor};"">
                                            <tr>
                                                {GenerateChronologicalTimelineSegments(employee.PresenceRecords)}
                                            </tr>
                                        </table>
                                    </td>
                                    <td style=""width: 80px; text-align: right; vertical-align: middle;"">
                                        <p style=""margin: 0; color: #0f5132; font-size: 16px; font-family: 'Courier New', monospace; font-weight: 700;"">{totalOnlineHours:F1}h</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>";
    }

    private string GenerateChronologicalTimelineSegments(List<ProjectCallisto.Domain.Organisations.PresenceHistory> presenceRecords)
    {
        if (!presenceRecords.Any())
        {
            // No data - show full offline bar
            return @"<td style=""width: 100%; background-color: #d1d5db; height: 18px;""></td>";
        }

        var segments = new StringBuilder();
        var sortedRecords = presenceRecords.OrderBy(r => r.RecordedAt).ToList();

        // Assume the period spans 24 hours
        var periodStart = sortedRecords.First().RecordedAt.Date;
        var periodEnd = periodStart.AddDays(1);
        var totalMinutes = 24 * 60.0;

        // Build segments from presence records
        for (int i = 0; i < sortedRecords.Count; i++)
        {
            var current = sortedRecords[i];
            var nextRecord = i < sortedRecords.Count - 1 ? sortedRecords[i + 1] : null;

            // Calculate segment duration
            var segmentStart = current.RecordedAt;
            var segmentEnd = nextRecord?.RecordedAt ?? periodEnd;

            var durationMinutes = (segmentEnd - segmentStart).TotalMinutes;
            var widthPercent = (durationMinutes / totalMinutes) * 100.0;

            // Only show segments that are visible (> 0.5%)
            if (widthPercent < 0.5) continue;

            var color = GetPresenceColor(current.Availability);
            segments.Append($@"<td style=""width: {widthPercent:F2}%; background-color: {color}; height: 18px;""></td>");
        }

        return segments.ToString();
    }

    private string GetPresenceColor(string availability)
    {
        return availability.ToLowerInvariant() switch
        {
            "available" or "availableidle" => "#22c55e", // Green
            "busy" or "busyidle" => "#ef4444", // Red
            "away" => "#f59e0b", // Orange
            "donotdisturb" => "#8b5cf6", // Purple
            _ => "#d1d5db" // Gray (Offline/Unknown)
        };
    }

    private string GenerateTimelineSegments(TimeBreakdown breakdown)
    {
        var segments = new StringBuilder();

        // For 24-hour timeline, show all presence states in order of percentage
        // Offline first, then Available, Busy, Away, DoNotDisturb

        if (breakdown.OfflinePercent > 0)
            segments.Append($@"<td style=""width: {breakdown.OfflinePercent}%; background-color: #d1d5db; height: 18px;""></td>");

        if (breakdown.AvailablePercent > 0)
            segments.Append($@"<td style=""width: {breakdown.AvailablePercent}%; background-color: #22c55e; height: 18px;""></td>");

        if (breakdown.BusyPercent > 0)
            segments.Append($@"<td style=""width: {breakdown.BusyPercent}%; background-color: #ef4444; height: 18px;""></td>");

        if (breakdown.AwayPercent > 0)
            segments.Append($@"<td style=""width: {breakdown.AwayPercent}%; background-color: #f59e0b; height: 18px;""></td>");

        if (breakdown.DoNotDisturbPercent > 0)
            segments.Append($@"<td style=""width: {breakdown.DoNotDisturbPercent}%; background-color: #8b5cf6; height: 18px;""></td>");

        return segments.ToString();
    }

    public string GenerateWeeklyReportHtml(
        string orgName,
        string reportPeriod,
        int trackedCount,
        List<EmployeePresenceBreakdown> employees,
        DateTime generatedAt)
    {
        var html = new StringBuilder();

        html.Append($@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Weekly Presence Report</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f5f7f6; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif;"">

    <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color: #f5f7f6;"">
        <tr>
            <td style=""padding: 40px 20px;"">

                <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""760"" style=""margin: 0 auto; background-color: #ffffff; box-shadow: 0 2px 8px rgba(0,0,0,0.06);"">

                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #0a3d28 0%, #0f5132 100%); padding: 28px 32px; border-bottom: 3px solid #ffc107;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 24px; font-weight: 700; letter-spacing: -0.3px;"">
                                Weekly Presence Report
                            </h1>
                            <p style=""margin: 4px 0 0 0; color: #a7f3d0; font-size: 13px;"">
                                {orgName} · {reportPeriod}
                            </p>
                        </td>
                    </tr>

                    <!-- Greeting -->
                    <tr>
                        <td style=""padding: 24px 32px 16px 32px;"">
                            <p style=""margin: 0; color: #1f2937; font-size: 14px;"">
                                Hello,
                            </p>
                            <p style=""margin: 8px 0 0 0; color: #4b5563; font-size: 13px; line-height: 1.6;"">
                                Here's your team's weekly summary with behavioral insights.
                            </p>
                        </td>
                    </tr>

                    <!-- Column Headers -->
                    <tr>
                        <td style=""padding: 16px 32px 12px 32px;"">
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"">
                                <tr>
                                    <td style=""width: 28%;"">
                                        <h2 style=""margin: 0; color: #111827; font-size: 15px; font-weight: 700;"">
                                            Team ({trackedCount} members)
                                        </h2>
                                    </td>
                                    <td style=""width: 36%; text-align: center;"">
                                        <p style=""margin: 0; color: #6b7280; font-size: 10px; text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600;"">Working Hours</p>
                                    </td>
                                    <td style=""width: 36%; text-align: center;"">
                                        <p style=""margin: 0; color: #6b7280; font-size: 10px; text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600;"">Full Week</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Employee Rows -->
");

        foreach (var employee in employees)
        {
            html.Append(GenerateWeeklyEmployeeRow(employee));
        }

        html.Append($@"
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f9fafb; padding: 20px 32px; border-top: 1px solid #e5e7eb;"">
                            <p style=""margin: 0 0 6px 0; color: #6b7280; font-size: 11px;"">
                                <strong style=""color: #111827;"">The Green Dots</strong> · Presence Analytics
                            </p>
                            <p style=""margin: 0; color: #9ca3af; font-size: 10px; line-height: 1.5;"">
                                Report generated on {generatedAt:MMMM d, yyyy} at {generatedAt:h:mm tt} UTC<br>
                                Showing data for: {reportPeriod}
                            </p>
                            <p style=""margin: 10px 0 0 0;"">
                                <a href=""https://thegreendots.app/settings/email-preferences"" style=""color: #0f5132; font-size: 10px; text-decoration: underline;"">Manage email preferences</a>
                            </p>
                        </td>
                    </tr>

                </table>

            </td>
        </tr>
    </table>

</body>
</html>");

        return html.ToString();
    }

    private string GenerateWeeklyEmployeeRow(EmployeePresenceBreakdown employee)
    {
        var workingHours = employee.WorkingHoursBreakdown;
        var fullWeek = employee.FullWeekBreakdown;
        var hasInsights = employee.Insights.Any();

        var bgColor = hasInsights ? "#fffbeb" : "#fafbfc";
        var borderColor = hasInsights ? "#fbbf24" : "#e5e7eb";
        var borderWidth = hasInsights ? "2px" : "1px";

        var insightHtml = hasInsights
            ? $@"<p style=""margin: 4px 0 0 0; color: #d97706; font-size: 10px; font-weight: 600;"">⚠️ {string.Join(", ", employee.Insights.Select(i => i.Message))}</p>"
            : "";

        var overtimeText = employee.OvertimeHours > 0
            ? $@" <span style=""font-size: 11px; font-weight: 600; color: #d97706;"">(+{employee.OvertimeHours:F1}h)</span>"
            : "";

        return $@"
                    <tr>
                        <td style=""padding: 0 32px 10px 32px;"">
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""background-color: {bgColor}; border: {borderWidth} solid {borderColor}; border-radius: 4px; padding: 12px;"">
                                <tr>
                                    <!-- Name Column -->
                                    <td style=""width: 28%; vertical-align: middle; padding-right: 12px;"">
                                        <p style=""margin: 0; color: #111827; font-size: 13px; font-weight: 600;"">{employee.DisplayName}</p>
                                        <p style=""margin: 2px 0 0 0; color: #6b7280; font-size: 10px;"">{employee.Email}</p>
                                        {insightHtml}
                                    </td>

                                    <!-- Working Hours Column -->
                                    <td style=""width: 36%; vertical-align: middle; padding: 0 8px;"">
                                        <p style=""margin: 0 0 6px 0; color: #0f5132; font-size: 13px; font-weight: 700;"">{workingHours.TotalHours:F1}h</p>
                                        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""height: 20px; border-radius: 4px; overflow: hidden; border: 1px solid {borderColor};"">
                                            <tr>
                                                {GenerateStackedBar(workingHours)}
                                            </tr>
                                        </table>
                                        <p style=""margin: 4px 0 0 0; color: #6b7280; font-size: 10px; line-height: 1.3;"">
                                            {workingHours.AvailableHours:F1}h · {workingHours.BusyHours:F1}h · {workingHours.AwayHours:F1}h · {workingHours.DoNotDisturbHours:F1}h
                                        </p>
                                    </td>

                                    <!-- Full Week Column -->
                                    <td style=""width: 36%; vertical-align: middle; padding: 0 8px;"">
                                        <p style=""margin: 0 0 6px 0; color: #6b7280; font-size: 13px; font-weight: 700;"">{fullWeek.TotalHours:F1}h{overtimeText}</p>
                                        <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" width=""100%"" style=""height: 20px; border-radius: 4px; overflow: hidden; border: 1px solid {borderColor};"">
                                            <tr>
                                                {GenerateStackedBar(fullWeek)}
                                            </tr>
                                        </table>
                                        <p style=""margin: 4px 0 0 0; color: #6b7280; font-size: 10px; line-height: 1.3;"">
                                            {fullWeek.AvailableHours:F1}h · {fullWeek.BusyHours:F1}h · {fullWeek.AwayHours:F1}h · {fullWeek.DoNotDisturbHours:F1}h
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>";
    }

    private string GenerateStackedBar(TimeBreakdown breakdown)
    {
        var segments = new StringBuilder();

        if (breakdown.AvailablePercent > 0)
            segments.Append($@"<td style=""width: {breakdown.AvailablePercent}%; background-color: #22c55e; padding: 0;""></td>");

        if (breakdown.BusyPercent > 0)
            segments.Append($@"<td style=""width: {breakdown.BusyPercent}%; background-color: #ef4444; padding: 0;""></td>");

        if (breakdown.AwayPercent > 0)
            segments.Append($@"<td style=""width: {breakdown.AwayPercent}%; background-color: #f59e0b; padding: 0;""></td>");

        if (breakdown.DoNotDisturbPercent > 0)
            segments.Append($@"<td style=""width: {breakdown.DoNotDisturbPercent}%; background-color: #8b5cf6; padding: 0;""></td>");

        if (breakdown.OfflinePercent > 0)
            segments.Append($@"<td style=""width: {breakdown.OfflinePercent}%; background-color: #d1d5db; padding: 0;""></td>");

        return segments.ToString();
    }

    public string GenerateMonthlyReportHtml(
        string orgName,
        string reportPeriod,
        int trackedCount,
        List<EmployeePresenceBreakdown> employees,
        DateTime generatedAt)
    {
        // Monthly report uses same structure as weekly, just different title and period
        var html = GenerateWeeklyReportHtml(orgName, reportPeriod, trackedCount, employees, generatedAt);
        return html.Replace("Weekly Presence Report", "Monthly Presence Report")
                   .Replace("weekly summary", "monthly summary");
    }
}
