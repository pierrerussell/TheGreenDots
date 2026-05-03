using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Queues;
using ProjectCallisto.Application.Reports;
using ProjectCallisto.Application.Reports.Models;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;
using ReportCalculationJob = ProjectCallisto.Application.Reports.Models.ReportCalculationJob;
using EmailRecipientDto = ProjectCallisto.Application.Reports.Models.EmailRecipientDto;

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
        _logger.LogInformation("Report scheduling triggered at: {time}", DateTimeOffset.UtcNow);

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queueService = scope.ServiceProvider.GetRequiredService<IQueueService<ReportCalculationJob>>();

        // Query all enabled reports
        var enabledReports = await dbContext.EmailReportSettings
            .Include(s => s.Recipients)
            .Include(s => s.Organisation)
            .Where(s => s.IsEnabled && s.Recipients.Any())
            .ToListAsync(ct);

        var currentUtc = DateTimeOffset.UtcNow;

        foreach (var settings in enabledReports)
        {
            try
            {
                if (IsReportDue(settings, settings.Organisation, currentUtc))
                {
                    var job = new ReportCalculationJob
                    {
                        EmailReportSettingsId = settings.Id,
                        OrganisationId = settings.OrganisationId,
                        OrganisationName = settings.Organisation.Name,
                        Frequency = settings.Frequency.ToString(), // "Daily", "Weekly", "Monthly"
                        Recipients = settings.Recipients.Select(r => new EmailRecipientDto
                        {
                            Email = r.Email,
                            Name = r.Name
                        }).ToList()
                    };

                    await queueService.EnqueueAsync(job);

                    // Update LastSentAt immediately (idempotency)
                    settings.LastSentAt = currentUtc;
                    await dbContext.SaveChangesAsync(ct);

                    _logger.LogInformation("Enqueued {Frequency} report for org: {OrgId}",
                        settings.Frequency, settings.OrganisationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling report for org: {OrgId}", settings.OrganisationId);
            }
        }
    }

    private bool IsReportDue(EmailReportSettings settings, Organisation org, DateTimeOffset currentUtc)
    {
        // 1. Basic guards
        if (!settings.IsEnabled || !settings.Recipients.Any()) return false;
        if (string.IsNullOrEmpty(org.Timezone)) return false;

        // 2. Convert to org timezone
        var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(org.Timezone);
        var localNow = TimeZoneInfo.ConvertTime(currentUtc, tzInfo);

        // 3. Exact hour match (user can only set hourly boundaries)
        if (localNow.Hour != settings.TimeOfDay.Hour) return false;

        // 4. Frequency-specific day checks
        switch (settings.Frequency)
        {
            case ReportFrequency.Daily:
                // Daily: Send every day at TimeOfDay
                break;

            case ReportFrequency.Weekly:
                // Weekly: Only send on configured DayOfWeek
                if (settings.DayOfWeek == null || localNow.DayOfWeek != settings.DayOfWeek.Value)
                    return false;
                break;

            case ReportFrequency.Monthly:
                // Monthly: Only send on configured DayOfMonth
                var targetDay = settings.DayOfMonth ?? 1;
                var daysInMonth = DateTime.DaysInMonth(localNow.Year, localNow.Month);
                var effectiveTargetDay = Math.Min(targetDay, daysInMonth);
                if (localNow.Day != effectiveTargetDay)
                    return false;
                break;
        }

        // 5. Idempotency check (prevent duplicate sends within 23 hours)
        if (settings.LastSentAt.HasValue)
        {
            var hoursSinceLastSend = (currentUtc - settings.LastSentAt.Value).TotalHours;
            if (hoursSinceLastSend < 23) return false;
        }

        return true;
    }
}