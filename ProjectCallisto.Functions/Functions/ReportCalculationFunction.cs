using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Emails;
using ProjectCallisto.Application.Queues;
using ProjectCallisto.Application.Reports;
using ProjectCallisto.Application.Reports.Models;
using ReportCalculationJob = ProjectCallisto.Application.Reports.Models.ReportCalculationJob;

namespace ProjectCallisto.Functions.Functions;

public class ReportCalculationFunction
{
    private readonly ILogger<ReportCalculationFunction> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ReportCalculationFunction(
        ILogger<ReportCalculationFunction> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    [Function("ReportCalculation")]
    public async Task Run(
        [QueueTrigger("report-calculation-queue", Connection = "AzureQueue")]
        string message,
        CancellationToken ct)
    {
        var job = JsonSerializer.Deserialize<ReportCalculationJob>(message);
        _logger.LogInformation("Calculating {Frequency} report for org: {OrgId}",
            job?.Frequency, job?.OrganisationId);

        using var scope = _serviceScopeFactory.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportCalculationService>();
        var emailQueue = scope.ServiceProvider.GetRequiredService<IQueueService<EmailMessage>>();
        var htmlGenerator = scope.ServiceProvider.GetRequiredService<ReportEmailHtmlGenerator>();

        // Calculate report based on frequency
        string htmlBody;
        string subject;

        switch (job!.Frequency)
        {
            case "Daily":
            {
                var report = await reportService.CalculateDailyReportAsync(job.OrganisationId, ct);
                var reportDate = report.StartDate.ToString("dddd, MMMM d, yyyy");
                var reportPeriod = report.StartDate.ToString("MMMM d, yyyy");

                htmlBody = htmlGenerator.GenerateDailyReportHtml(
                    report.OrganisationName,
                    reportDate,
                    reportPeriod,
                    report.TotalMembers,
                    report.Employees,
                    DateTime.UtcNow);

                subject = $"Daily Presence Report - {reportPeriod}";
                break;
            }

            case "Weekly":
            {
                var report = await reportService.CalculateWeeklyReportAsync(job.OrganisationId, ct);
                var reportPeriod = $"{report.StartDate:MMMM d} - {report.EndDate:MMMM d, yyyy}";

                htmlBody = htmlGenerator.GenerateWeeklyReportHtml(
                    report.OrganisationName,
                    reportPeriod,
                    report.TotalMembers,
                    report.Employees,
                    DateTime.UtcNow,
                    report.WorkingHours);

                subject = $"Weekly Presence Report - {reportPeriod}";
                break;
            }

            case "Monthly":
            {
                var report = await reportService.CalculateMonthlyReportAsync(job.OrganisationId, ct);
                var reportPeriod = $"{report.StartDate:MMMM yyyy}";

                htmlBody = htmlGenerator.GenerateMonthlyReportHtml(
                    report.OrganisationName,
                    reportPeriod,
                    report.TotalMembers,
                    report.Employees,
                    DateTime.UtcNow,
                    report.WorkingHours);

                subject = $"Monthly Presence Report - {reportPeriod}";
                break;
            }

            default:
                throw new InvalidOperationException($"Unknown frequency: {job.Frequency}");
        }

        // Enqueue email for each recipient
        foreach (var recipient in job.Recipients)
        {
            var emailMessage = new EmailMessage
            {
                To = recipient.Email,
                Subject = subject,
                HtmlBody = htmlBody
            };

            await emailQueue.EnqueueAsync(emailMessage);
        }

        _logger.LogInformation("Enqueued {Count} emails for {Frequency} report (org: {OrgId})",
            job.Recipients.Count, job.Frequency, job.OrganisationId);
    }
}