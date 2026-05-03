using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Queues;
using ProjectCallisto.Application.Reports;

namespace ProjectCallisto.Functions.Functions;

public class ReportSchedulingFunction
{
    private readonly ILogger<ReportSchedulingFunction> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ReportSchedulingFunction(
        ILogger<ReportSchedulingFunction> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    [Function("ReportScheduling")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timerInfo, CancellationToken ct)
    {
        _logger.LogInformation("Report scheduling function triggered at: {time}", DateTimeOffset.UtcNow);

        // DUMMY IMPLEMENTATION - just enqueue a test job
        // using var scope = _serviceScopeFactory.CreateScope();
        // var queueService = scope.ServiceProvider.GetRequiredService<IQueueService<ReportCalculationJob>>();
        //
        // var sampleOrg = Guid.NewGuid();
        // var testJob = new ReportCalculationJob
        // {
        //     EmailReportSettingsId = Guid.NewGuid(),
        //     OrganisationId = sampleOrg,
        //     OrganisationName = "Test Organization " + sampleOrg.ToString(),
        //     Frequency = "Weekly"
        // };
        //
        // await queueService.EnqueueAsync(testJob);
        _logger.LogInformation("Enqueued test report calculation job");
    }
}