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

    [HttpGet("{id:guid}/access")]
    public async Task<IActionResult> CheckAccess(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var hasAccess = await _dbContext.OrganisationUsers
            .AnyAsync(ou => ou.UserId == user.Id && ou.OrganisationId == id);

        if (!hasAccess)
        {
            return Ok(new { hasAccess = false, role = (string?)null });
        }

        // For now, everyone is a member. Add role logic later if needed
        // TODO add role logic once role table is created.
        return Ok(new { hasAccess = true, role = "member" });
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
    public async Task<IActionResult> GetPresenceTimeline(Guid id, [FromQuery] DateTime startTime, [FromQuery] DateTime endTime)
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

        // Convert to DateTimeOffset for consistency with database
        var dayStart = new DateTimeOffset(startTime, TimeSpan.Zero);
        var dayEnd = new DateTimeOffset(endTime, TimeSpan.Zero);
        // Query from 1 hour before to ensure we have starting state
        var queryStart = dayStart.AddHours(-1);

        var historyRecords = await _dbContext.PresenceHistories
            .Where(ph => memberIds.Contains(ph.TenantMemberId) && ph.RecordedAt >= queryStart && ph.RecordedAt <= dayEnd)
            .OrderBy(ph => ph.RecordedAt)
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

            if (memberHistory.Count == 0)
            {
                result.Add(new MemberTimelineResponse(member.Id, member.DisplayName, entries));
                continue;
            }

            // Find records before and after midnight
            var recordsBeforeMidnight = memberHistory.Where(h => h.RecordedAt < dayStart).ToList();
            var recordsAfterMidnight = memberHistory.Where(h => h.RecordedAt >= dayStart).ToList();

            // If there's a status from before midnight, use it as starting point
            if (recordsBeforeMidnight.Count > 0)
            {
                var lastBeforeMidnight = recordsBeforeMidnight.Last();

                if (recordsAfterMidnight.Count > 0)
                {
                    // Add segment from midnight to first change after midnight
                    var firstAfterMidnight = recordsAfterMidnight[0];
                    entries.Add(new TimelineEntry(
                        lastBeforeMidnight.Availability,
                        dayStart,
                        firstAfterMidnight.RecordedAt,
                        (int)(firstAfterMidnight.RecordedAt - dayStart).TotalMinutes
                    ));
                }
                else
                {
                    // No changes after midnight - single segment for whole day
                    var effectiveEnd = dayEnd < now ? dayEnd : now;
                    entries.Add(new TimelineEntry(
                        lastBeforeMidnight.Availability,
                        dayStart,
                        effectiveEnd,
                        (int)(effectiveEnd - dayStart).TotalMinutes
                    ));
                }
            }

            // Process each status change after midnight
            for (int i = 0; i < recordsAfterMidnight.Count; i++)
            {
                var current = recordsAfterMidnight[i];
                var next = i + 1 < recordsAfterMidnight.Count ? recordsAfterMidnight[i + 1] : null;

                DateTimeOffset? segmentEndTime = next?.RecordedAt;
                int durationMinutes;

                if (segmentEndTime.HasValue)
                {
                    durationMinutes = (int)(segmentEndTime.Value - current.RecordedAt).TotalMinutes;
                }
                else
                {
                    // Last segment extends to now (or end of day if viewing past date)
                    var effectiveEnd = dayEnd < now ? dayEnd : now;
                    durationMinutes = (int)(effectiveEnd - current.RecordedAt).TotalMinutes;
                }

                entries.Add(new TimelineEntry(
                    current.Availability,
                    current.RecordedAt,
                    segmentEndTime,
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
