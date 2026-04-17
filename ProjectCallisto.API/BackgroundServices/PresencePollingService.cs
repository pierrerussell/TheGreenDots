using Microsoft.EntityFrameworkCore;
using ProjectCallisto.API.Services;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.BackgroundServices;

public class PresencePollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PresencePollingService> _logger;

    public PresencePollingService(IServiceScopeFactory scopeFactory, ILogger<PresencePollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _ = PollAllOrganisationsAsync(stoppingToken);
        }
    }

    private async Task PollAllOrganisationsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeConnections = await dbContext.Organisations
                .Join(
                    dbContext.MicrosoftConnections,
                    org => org.ActiveConnectionId,
                    conn => conn.Id,
                    (org, conn) => new ActiveConnection
                    {
                        OrganisationId = org.Id,
                        ConnectionId = conn.Id,
                        TenantId = org.TenantId,
                        TenantName = org.Name
                    })
                .ToListAsync(ct);

            var tasks = activeConnections.Select(conn => PollOrganisationAsync(conn, ct));
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling organisations");
        }
    }

    private async Task PollOrganisationAsync(ActiveConnection conn, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IMicrosoftTokenService>();
            var graphService = scope.ServiceProvider.GetRequiredService<IMicrosoftGraphService>();

            // Get valid (refreshed if needed) connection
            var connection = await tokenService.GetValidConnectionAsync(conn.ConnectionId, ct);
            if (connection == null)
            {
                _logger.LogWarning("Connection {ConnectionId} not found", conn.ConnectionId);
                return;
            }

            // Get all members for this organisation
            var members = await dbContext.TenantMembers
                .Where(m => m.OrganisationId == conn.OrganisationId)
                .ToListAsync(ct);

            if (members.Count == 0) return;

            // Fetch current presence from Microsoft Graph
            var memberIds = members.Select(m => m.MicrosoftUserId).ToList();
            var presenceMap = await graphService.GetPresenceAsync(connection, memberIds);

            // Get the last recorded status for each member
            var lastStatuses = await dbContext.PresenceHistories
                .Where(ph => members.Select(m => m.Id).Contains(ph.TenantMemberId))
                .GroupBy(ph => ph.TenantMemberId)
                .Select(g => g.OrderByDescending(ph => ph.RecordedAt).First())
                .ToDictionaryAsync(ph => ph.TenantMemberId, ct);

            // Record changes (when availability OR activity changes OR last record was >1 hour ago)
            var now = DateTimeOffset.UtcNow;
            var newRecords = new List<PresenceHistory>();

            foreach (var member in members)
            {
                if (!presenceMap.TryGetValue(member.MicrosoftUserId, out var currentPresence))
                    continue;

                var shouldStore = !lastStatuses.TryGetValue(member.Id, out var lastStatus) // No previous record
                    || lastStatus.Availability != currentPresence.Availability // Availability changed
                    || lastStatus.Activity != (currentPresence.Activity ?? string.Empty) // Activity changed
                    || (now - lastStatus.RecordedAt).TotalHours >= 1; // At least 1 hour since last record

                if (shouldStore)
                {
                    newRecords.Add(new PresenceHistory
                    {
                        Id = Guid.NewGuid(),
                        TenantMemberId = member.Id,
                        Availability = currentPresence.Availability,
                        Activity = currentPresence.Activity ?? string.Empty,
                        RecordedAt = now
                    });
                }
            }

            if (newRecords.Count > 0)
            {
                dbContext.PresenceHistories.AddRange(newRecords);
                await dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Recorded {Count} presence changes for {OrgName}", newRecords.Count, conn.TenantName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling organisation {OrganisationId}", conn.OrganisationId);
        }
    }

    private class ActiveConnection
    {
        public Guid OrganisationId { get; init; }
        public Guid ConnectionId { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public string TenantName { get; init; } = string.Empty;
    }
}