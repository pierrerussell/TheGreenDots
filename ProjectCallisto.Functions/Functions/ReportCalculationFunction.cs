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

        // Calculate report based on frequency
        object reportData;
        string templateId;

        switch (job!.Frequency)
        {
            case "Daily":
                reportData = await reportService.CalculateDailyReportAsync(job.OrganisationId, ct);
                templateId = "daily-presence-report"; // Future template
                break;

            case "Weekly":
                reportData = await reportService.CalculateWeeklyReportAsync(job.OrganisationId, ct);
                templateId = "weekly-presence-report";
                break;

            case "Monthly":
                reportData = await reportService.CalculateMonthlyReportAsync(job.OrganisationId, ct);
                templateId = "monthly-presence-report"; // Future template
                break;

            default:
                throw new InvalidOperationException($"Unknown frequency: {job.Frequency}");
        }

        // Cast to common interface (they all have same properties for now)
        dynamic report = reportData;

        // Enqueue email for each recipient
        foreach (var recipient in job.Recipients)
        {
            var emailMessage = new EmailMessage
            {
                To = recipient.Email,
                TemplateId = templateId,
                TemplateData = new Dictionary<string, object>
                {
                    { "organization_name", report.OrganisationName },
                    { "report_start", report.StartDate.ToString("MMMM dd, yyyy") },
                    { "report_end", report.EndDate.ToString("MMMM dd, yyyy") },
                    { "recipient_name", recipient.Name ?? recipient.Email },
                    { "total_members", report.TotalMembers },
                    { "frequency", job.Frequency.ToLower() }
                    // TODO: Add employee rows HTML generation
                }
            };

            await emailQueue.EnqueueAsync(emailMessage);
        }

        _logger.LogInformation("Enqueued {Count} emails for {Frequency} report (org: {OrgId})",
            job.Recipients.Count, job.Frequency, job.OrganisationId);
    }
}