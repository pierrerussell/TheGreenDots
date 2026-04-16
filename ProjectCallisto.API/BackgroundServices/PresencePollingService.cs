using Microsoft.EntityFrameworkCore;
using ProjectCallisto.API.Services;
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
            var tokenService = scope.ServiceProvider.GetRequiredService<IMicrosoftTokenService>();

            // Get valid (refreshed if needed) connection
            var connection = await tokenService.GetValidConnectionAsync(conn.ConnectionId, ct);
            if (connection == null)
            {
                _logger.LogWarning("Connection {ConnectionId} not found", conn.ConnectionId);
                return;
            }

            // TODO: Poll presence using connection.AccessToken
            _logger.LogInformation("Polling organisation {OrganisationId}, Organisation Name: {OrganisationName}", conn.OrganisationId, conn.TenantName);
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