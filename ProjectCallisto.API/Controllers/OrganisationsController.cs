using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.API.Authorization;
using ProjectCallisto.Application.Microsoft;
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
    [Authorize(Policy = nameof(Permission.ViewDashboard))]
    public async Task<IActionResult> GetOrganisation(Guid id)
    {
        // Authorization already handled by policy - user has access to this org
        var org = await _dbContext.Organisations
            .FirstOrDefaultAsync(o => o.Id == id);

        if (org == null) return NotFound();
        return Ok(org);
    }

    [HttpGet("{id:guid}/access")]
    public async Task<IActionResult> CheckAccess(Guid id)
    {
        // Temporarily removed authorization to debug - check manually
        var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        Console.WriteLine($"[DEBUG] CheckAccess called for org {id}");
        Console.WriteLine($"[DEBUG] SubjectId from claims: {subjectId}");

        var user = await GetCurrentUserAsync();
        Console.WriteLine($"[DEBUG] User lookup result: {(user != null ? $"Found (Id: {user.Id}, Email: {user.Email})" : "NULL")}");

        if (user == null) return Unauthorized();

        var orgUser = await _dbContext.OrganisationUsers
            .FirstOrDefaultAsync(ou => ou.UserId == user.Id && ou.OrganisationId == id);

        Console.WriteLine($"[DEBUG] OrgUser lookup result: {(orgUser != null ? $"Found (Role: {orgUser.Role})" : "NULL")}");

        if (orgUser == null)
        {
            Console.WriteLine($"[DEBUG] Returning hasAccess=false - no OrganisationUser record found");
            return Ok(new { hasAccess = false, role = (string?)null });
        }

        var roleLower = orgUser.Role.ToString().ToLower();
        Console.WriteLine($"[DEBUG] Returning hasAccess=true, role={roleLower}");
        return Ok(new { hasAccess = true, role = roleLower });
    }

    [HttpGet("{id:guid}/debug-auth")]
    public async Task<IActionResult> DebugAuth(Guid id)
    {
        var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var user = await GetCurrentUserAsync();

        var orgUser = user != null
            ? await _dbContext.OrganisationUsers.FirstOrDefaultAsync(ou => ou.UserId == user.Id && ou.OrganisationId == id)
            : null;

        return Ok(new
        {
            subjectId,
            userId = user?.Id,
            userEmail = user?.Email,
            orgUserId = orgUser?.UserId,
            orgUserRole = orgUser?.Role.ToString(),
            orgUserRoleLower = orgUser?.Role.ToString().ToLower(),
            hasViewDashboardPermission = orgUser != null && RolePermissions.HasPermission(orgUser.Role, Permission.ViewDashboard),
            hasManageSeatsPermission = orgUser != null && RolePermissions.HasPermission(orgUser.Role, Permission.ManageSeats),
            allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> DeleteOrganisation(Guid id)
    {
        // Authorization already verified user is admin with ManageSettings permission
        var org = await _dbContext.Organisations
            .FirstOrDefaultAsync(o => o.Id == id);

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
    [Authorize(Policy = nameof(Permission.ViewDashboard))]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        // Authorization already verified access - get only assigned seat members
        var members = await _dbContext.TenantMembers
            .Where(m => m.OrganisationId == id && m.IsAssignedSeat)
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
    [Authorize(Policy = nameof(Permission.ViewDashboard))]
    public async Task<IActionResult> GetPresenceHistory(Guid id, [FromQuery] int limit = 100)
    {
        // Authorization already verified access - get only assigned seat members
        var memberIds = await _dbContext.TenantMembers
            .Where(m => m.OrganisationId == id && m.IsAssignedSeat)
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
    [Authorize(Policy = nameof(Permission.ViewDashboard))]
    public async Task<IActionResult> GetPresenceTimeline(Guid id, [FromQuery] DateTime startTime, [FromQuery] DateTime endTime)
    {
        // Authorization already verified access - get only assigned seat members
        var members = await _dbContext.TenantMembers
            .Where(m => m.OrganisationId == id && m.IsAssignedSeat)
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

    [HttpGet("{id:guid}/all-members")]
    [Authorize(Policy = nameof(Permission.ViewDashboard))]
    public async Task<IActionResult> GetAllMembers(Guid id)
    {
        // Returns ALL members (not filtered by IsAssignedSeat)
        // Used by: People page (all users), Settings page (admins only for seat management)
        var members = await _dbContext.TenantMembers
            .Where(m => m.OrganisationId == id)
            .Select(m => new AllMembersResponse(
                m.Id,
                m.DisplayName,
                m.Email,
                m.JobTitle,
                m.IsAssignedSeat
            ))
            .ToListAsync();

        return Ok(members);
    }

    [HttpPost("{id:guid}/members/{memberId:guid}/assign-seat")]
    [Authorize(Policy = nameof(Permission.ManageSeats))] 
    public async Task<IActionResult> AssignSeat(Guid id, Guid memberId)
    {
        // Authorization already verified user has ManageSeats permission

        // Get subscription to check seat limit
        var organisation = await _dbContext.Organisations
            .FirstOrDefaultAsync(o => o.Id == id);

        if (organisation?.Subscription == null)
            return NotFound("Organisation or subscription not found");

        // Check seat limit
        var assignedCount = await _dbContext.TenantMembers
            .CountAsync(m => m.OrganisationId == id && m.IsAssignedSeat);

        if (assignedCount >= organisation.Subscription.PaidSeats)
        {
            return BadRequest(new { error = "No seats available. Upgrade your plan to assign more members." });
        }

        // Assign seat
        var member = await _dbContext.TenantMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganisationId == id);

        if (member == null)
            return NotFound("Member not found");

        if (member.IsAssignedSeat)
            return BadRequest(new { error = "Member already has an assigned seat" });

        member.IsAssignedSeat = true;

        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id:guid}/members/{memberId:guid}/unassign-seat")]
    [Authorize(Policy = nameof(Permission.ManageSeats))] 
    public async Task<IActionResult> UnassignSeat(Guid id, Guid memberId)
    {
        // Authorization already verified user has ManageSeats permission

        var member = await _dbContext.TenantMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganisationId == id);

        if (member == null)
            return NotFound("Member not found");

        if (!member.IsAssignedSeat)
            return BadRequest(new { error = "Member does not have an assigned seat" });

        member.IsAssignedSeat = false;

        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("{id:guid}/subscription")]
    [Authorize(Policy = nameof(Permission.ManageBilling))]
    public async Task<IActionResult> GetSubscription(Guid id)
    {
        // Authorization already verified user has ManageBilling permission (admin-only)

        var organisation = await _dbContext.Organisations
            .FirstOrDefaultAsync(o => o.Id == id);

        if (organisation?.Subscription == null)
            return NotFound("Organisation or subscription not found");

        var assignedSeats = await _dbContext.TenantMembers
            .CountAsync(m => m.OrganisationId == id && m.IsAssignedSeat);

        return Ok(new SubscriptionResponse(
            organisation.Subscription.Id,
            organisation.Subscription.Status.ToString().ToLowerInvariant(),
            organisation.Subscription.PaidSeats,
            assignedSeats,
            organisation.Subscription.TrialEndsAt,
            organisation.StripeCustomerId,
            organisation.Subscription.StripeSubscriptionId,
            organisation.Subscription.CreatedAt
        ));
    }

    [HttpGet("{id:guid}/timezone")]
    [Authorize(Policy = nameof(Permission.ViewDashboard))]
    public async Task<IActionResult> GetTimezone(Guid id)
    {
        var org = await _dbContext.Organisations
            .FirstOrDefaultAsync(o => o.Id == id);

        if (org == null) return NotFound();

        return Ok(new
        {
            country = org.Country,
            countryDetectedFrom = org.CountryDetectedFrom,
            timezone = org.Timezone,
            timezoneDetectedFrom = org.TimezoneDetectedFrom
        });
    }

    [HttpPut("{id:guid}/timezone")]
    [Authorize(Policy = nameof(Permission.ManageSettings))]
    public async Task<IActionResult> UpdateTimezone(
        Guid id,
        [FromBody] UpdateTimezoneRequest request)
    {
        var org = await _dbContext.Organisations
            .FirstOrDefaultAsync(o => o.Id == id);

        if (org == null) return NotFound();

        // Validate IANA timezone
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(request.Timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return BadRequest($"Invalid timezone: {request.Timezone}");
        }

        org.Timezone = request.Timezone;
        org.TimezoneDetectedFrom = "Manual"; // User override

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            country = org.Country,
            timezone = org.Timezone,
            timezoneDetectedFrom = org.TimezoneDetectedFrom
        });
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (subjectId == null) return null;
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId);
    }
}

public record UpdateTimezoneRequest(string Timezone);

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

public record AllMembersResponse(
    Guid Id,
    string DisplayName,
    string? Email,
    string? JobTitle,
    bool IsAssignedSeat
);

public record SubscriptionResponse(
    Guid Id,
    string Status,
    int PaidSeats,
    int AssignedSeats,
    DateTimeOffset? TrialEndsAt,
    string? StripeCustomerId,
    string? StripeSubscriptionId,
    DateTimeOffset CreatedAt
);
