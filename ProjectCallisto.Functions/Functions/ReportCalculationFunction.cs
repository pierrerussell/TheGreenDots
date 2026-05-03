using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Emails;
using ProjectCallisto.Application.Queues;
using ProjectCallisto.Application.Reports;

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
        _logger.LogInformation("Report calculation function triggered");
        //
        // // Deserialize job
        // var job = JsonSerializer.Deserialize<ReportCalculationJob>(message);
        // _logger.LogInformation(
        //     "Processing report for org: {OrgName}, Frequency: {Frequency}",
        //     job?.OrganisationName,
        //     job?.Frequency);
        //
        // // DUMMY IMPLEMENTATION - just log and enqueue a dummy email
        // using var scope = _serviceScopeFactory.CreateScope();
        // var emailQueue = scope.ServiceProvider.GetRequiredService<IQueueService<EmailMessage>>();
        //
        // var dummyEmail = new EmailMessage
        // {
        //     To = "pierrerussellhojun@gmail.com",
        //     TemplateId = "8560de20-9588-4165-a53a-dde51d237e4b",
        //     TemplateData = new Dictionary<string, object>
        //     {
        //         { "organization_name", job?.OrganisationName ?? "Unknown" },
        //         { "week_start", "May 1, 2026" },
        //         { "week_end", "May 7, 2026" }
        //     }
        // };
        //
        // await emailQueue.EnqueueAsync(dummyEmail);
        _logger.LogInformation("Enqueued dummy email for report");
    }
}