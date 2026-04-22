using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectCallisto.Application.Microsoft;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.Functions.Functions;

public class PresencePollingFunction
{
    
    private readonly ILogger<PresencePollingFunction> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IMicrosoftTokenService _tokenService;
    private readonly IMicrosoftGraphService  _graphService;
    
    public PresencePollingFunction(                                                                      
        ILogger<PresencePollingFunction> logger,                                                         
        AppDbContext dbContext,                                                                          
        IMicrosoftTokenService tokenService,                                                             
        IMicrosoftGraphService graphService)                                                             
    {                                                                                                    
        _logger = logger;                                                                                
        _dbContext = dbContext;                                                                          
        _tokenService = tokenService;                                                                    
        _graphService = graphService;                                                                    
    }           
    
    [Function("PresencePolling")]
    public async Task Run([TimerTrigger("*/15 * * * * *")] TimerInfo timerInfo, CancellationToken ct)
    {
        _logger.LogInformation("Presence polling function triggered at: {time}", DateTimeOffset.UtcNow);
        await PollAllOrganisationsAsync(ct);
    }
    
    private async Task PollAllOrganisationsAsync(CancellationToken ct)
    {
        try
        {
            
            var activeConnections = await _dbContext.Organisations
                .Join(
                    _dbContext.MicrosoftConnections,
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
            
            // Get valid (refreshed if needed) connection
            var connection = await _tokenService.GetValidConnectionAsync(conn.ConnectionId, ct);
            if (connection == null)
            {
                _logger.LogWarning("Connection {ConnectionId} not found", conn.ConnectionId);
                return;
            }

            // Get all members for this organisation
            var members = await _dbContext.TenantMembers
                .Where(m => m.OrganisationId == conn.OrganisationId)
                .ToListAsync(ct);

            if (members.Count == 0) return;

            // Fetch current presence from Microsoft Graph
            var memberIds = members.Select(m => m.MicrosoftUserId).ToList();
            var presenceMap = await _graphService.GetPresenceAsync(connection, memberIds);

            // Get the last recorded status for each member
            var lastStatuses = await _dbContext.PresenceHistories
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
                _dbContext.PresenceHistories.AddRange(newRecords);
                await _dbContext.SaveChangesAsync(ct);
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