using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.API.Services;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations")]
public class OrganisationsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IMicrosoftGraphService _graphService;

    public OrganisationsController(AppDbContext dbContext, IMicrosoftGraphService graphService)
    {
        _dbContext = dbContext;
        _graphService = graphService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrganisations()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var organisations = await _dbContext.OrganisationUsers
            .Where(ou => ou.UserId == user.Id)
            .Join(
                _dbContext.Organisations,
                ou => ou.OrganisationId,
                o => o.Id,
                (ou, o) => o)
            .ToListAsync();

        return Ok(organisations);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrganisation(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var org = await _dbContext.OrganisationUsers
            .Where(ou => ou.UserId == user.Id && ou.OrganisationId == id)
            .Join(
                _dbContext.Organisations,
                ou => ou.OrganisationId,
                o => o.Id,
                (ou, o) => o)
            .FirstOrDefaultAsync();

        if (org == null) return NotFound();
        return Ok(org);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteOrganisation(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var org = await _dbContext.OrganisationUsers
            .Where(ou => ou.UserId == user.Id && ou.OrganisationId == id)
            .Join(
                _dbContext.Organisations,
                ou => ou.OrganisationId,
                o => o.Id,
                (ou, o) => o)
            .FirstOrDefaultAsync();

        if (org == null) return NotFound();

        // Remove organisation user links
        var orgUsers = await _dbContext.OrganisationUsers
            .Where(ou => ou.OrganisationId == id)
            .ToListAsync();
        _dbContext.OrganisationUsers.RemoveRange(orgUsers);

        // Remove associated microsoft connections
        var connections = await _dbContext.MicrosoftConnections
            .Where(mc => mc.Id == org.ActiveConnectionId)
            .ToListAsync();
        _dbContext.MicrosoftConnections.RemoveRange(connections);

        // Remove organisation
        _dbContext.Organisations.Remove(org);

        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        // Verify access
        var hasAccess = await _dbContext.OrganisationUsers
            .AnyAsync(ou => ou.UserId == user.Id && ou.OrganisationId == id);
        if (!hasAccess) return NotFound();

        // Get members
        var members = await _dbContext.TenantMembers
            .Where(m => m.OrganisationId == id)
            .ToListAsync();

        // Get connection for Graph API call
        var connection = await _dbContext.Organisations
            .Where(o => o.Id == id)
            .Join(
                _dbContext.MicrosoftConnections,
                o => o.ActiveConnectionId,
                c => c.Id,
                (o, c) => c)
            .FirstOrDefaultAsync();

        if (connection == null)
            return Ok(members.Select(m => new MemberResponse(m.Id, m.DisplayName, m.Email, m.JobTitle, "Offline", null)));

        // Fetch live presence
        var memberIds = members.Select(m => m.MicrosoftUserId).ToList();
        var presenceMap = await _graphService.GetPresenceAsync(connection, memberIds);

        var result = members.Select(m => new MemberResponse(
            m.Id,
            m.DisplayName,
            m.Email,
            m.JobTitle,
            presenceMap.GetValueOrDefault(m.MicrosoftUserId)?.Availability ?? "Offline",
            presenceMap.GetValueOrDefault(m.MicrosoftUserId)?.Activity
        ));

        return Ok(result);
    }

    [HttpGet("{id:guid}/presence-history")]
    public async Task<IActionResult> GetPresenceHistory(Guid id, [FromQuery] int limit = 100)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        // Verify access
        var hasAccess = await _dbContext.OrganisationUsers
            .AnyAsync(ou => ou.UserId == user.Id && ou.OrganisationId == id);
        if (!hasAccess) return NotFound();

        // Get member IDs for this organisation
        var memberIds = await _dbContext.TenantMembers
            .Where(m => m.OrganisationId == id)
            .Select(m => m.Id)
            .ToListAsync();

        // Get presence history with member info
        var history = await _dbContext.PresenceHistories
            .Where(ph => memberIds.Contains(ph.TenantMemberId))
            .OrderByDescending(ph => ph.RecordedAt)
            .Take(limit)
            .Join(
                _dbContext.TenantMembers,
                ph => ph.TenantMemberId,
                tm => tm.Id,
                (ph, tm) => new PresenceHistoryResponse(
                    ph.Id,
                    ph.TenantMemberId,
                    tm.DisplayName,
                    ph.Availability,
                    ph.Activity,
                    ph.RecordedAt
                ))
            .ToListAsync();

        return Ok(history);
    }

    [HttpGet("{id:guid}/presence-timeline")]
    public async Task<IActionResult> GetPresenceTimeline(Guid id, [FromQuery] DateOnly date)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        // Verify access
        var hasAccess = await _dbContext.OrganisationUsers
            .AnyAsync(ou => ou.UserId == user.Id && ou.OrganisationId == id);
        if (!hasAccess) return NotFound();

        // Get members for this organisation
        var members = await _dbContext.TenantMembers
            .Where(m => m.OrganisationId == id)
            .ToListAsync();

        var memberIds = members.Select(m => m.Id).ToList();

        // Get presence history for the requested date
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var historyRecords = await _dbContext.PresenceHistories
            .Where(ph => memberIds.Contains(ph.TenantMemberId) && ph.RecordedAt >= dayStart && ph.RecordedAt <= dayEnd)
            .OrderBy(ph => ph.RecordedAt)
            .ToListAsync();

        // Also get the last status before the day started (to know starting state)
        var lastBeforeDay = await _dbContext.PresenceHistories
            .Where(ph => memberIds.Contains(ph.TenantMemberId) && ph.RecordedAt < dayStart)
            .GroupBy(ph => ph.TenantMemberId)
            .Select(g => g.OrderByDescending(ph => ph.RecordedAt).First())
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var result = new List<MemberTimelineResponse>();

        foreach (var member in members)
        {
            var memberHistory = historyRecords
                .Where(h => h.TenantMemberId == member.Id)
                .OrderBy(h => h.RecordedAt)
                .ToList();

            var entries = new List<TimelineEntry>();

            // If there's a status from before the day, use it as starting point
            var priorStatus = lastBeforeDay.FirstOrDefault(h => h.TenantMemberId == member.Id);
            if (priorStatus != null && memberHistory.Count > 0)
            {
                // Add segment from midnight to first change
                var firstChange = memberHistory[0];
                entries.Add(new TimelineEntry(
                    priorStatus.Availability,
                    dayStart,
                    firstChange.RecordedAt,
                    (int)(firstChange.RecordedAt - dayStart).TotalMinutes
                ));
            }

            // Process each status change
            for (int i = 0; i < memberHistory.Count; i++)
            {
                var current = memberHistory[i];
                var next = i + 1 < memberHistory.Count ? memberHistory[i + 1] : null;

                DateTimeOffset? endTime = next?.RecordedAt;
                int durationMinutes;

                if (endTime.HasValue)
                {
                    durationMinutes = (int)(endTime.Value - current.RecordedAt).TotalMinutes;
                }
                else
                {
                    // Last segment extends to now (or end of day if viewing past date)
                    var effectiveEnd = date < DateOnly.FromDateTime(DateTime.UtcNow)
                        ? dayEnd
                        : now;
                    durationMinutes = (int)(effectiveEnd - current.RecordedAt).TotalMinutes;
                }

                entries.Add(new TimelineEntry(
                    current.Availability,
                    current.RecordedAt,
                    endTime,
                    durationMinutes
                ));
            }

            result.Add(new MemberTimelineResponse(member.Id, member.DisplayName, entries));
        }

        return Ok(result);
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (subjectId == null) return null;
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId);
    }
}

public record MemberResponse(
    Guid Id,
    string DisplayName,
    string? Email,
    string? JobTitle,
    string Availability,
    string? Activity
);

public record PresenceHistoryResponse(
    Guid Id,
    Guid MemberId,
    string MemberName,
    string Availability,
    string Activity,
    DateTimeOffset RecordedAt
);

public record MemberTimelineResponse(
    Guid MemberId,
    string MemberName,
    List<TimelineEntry> Entries
);

public record TimelineEntry(
    string Status,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    int DurationMinutes
);
