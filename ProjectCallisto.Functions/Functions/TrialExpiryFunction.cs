using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.Functions.Functions;

public class TrialExpiryFunction
{
    private readonly ILogger<TrialExpiryFunction> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public TrialExpiryFunction(
        ILogger<TrialExpiryFunction> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    // Runs daily at 2 AM UTC (0 2 * * *)
    [Function("TrialExpiry")]
    public async Task Run([TimerTrigger("0 2 * * *")] TimerInfo timerInfo, CancellationToken ct)
    {
        _logger.LogInformation("Trial expiry check triggered at: {time}", DateTimeOffset.UtcNow);

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find all expired trials that haven't been processed yet
        var expiredTrials = await dbContext.Subscriptions
            .Include(s => s.Organisation)
            .Where(s => s.Status == SubscriptionStatus.Trial
                && s.TrialEndsAt.HasValue
                && s.TrialEndsAt.Value < DateTimeOffset.UtcNow
                && s.PaidSeats > 0) // Only process if not already zeroed out
            .ToListAsync(ct);

        if (!expiredTrials.Any())
        {
            _logger.LogInformation("No expired trials found");
            return;
        }

        _logger.LogInformation("Found {Count} expired trials to process", expiredTrials.Count);

        foreach (var subscription in expiredTrials)
        {
            try
            {
                _logger.LogInformation(
                    "Processing expired trial for organisation {OrgId} ({OrgName}), expired at: {ExpiryDate}",
                    subscription.OrganisationId,
                    subscription.Organisation.Name,
                    subscription.TrialEndsAt);

                // Set paid seats to 0
                subscription.PaidSeats = 0;

                // Unassign all seats for this organisation
                var members = await dbContext.TenantMembers
                    .Where(tm => tm.OrganisationId == subscription.OrganisationId && tm.IsAssignedSeat)
                    .ToListAsync(ct);

                foreach (var member in members)
                {
                    member.IsAssignedSeat = false;
                }

                await dbContext.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Expired trial processed: Organisation {OrgId}, unassigned {Count} seats",
                    subscription.OrganisationId,
                    members.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing expired trial for organisation {OrgId}",
                    subscription.OrganisationId);
            }
        }

        _logger.LogInformation("Trial expiry processing complete");
    }
}
