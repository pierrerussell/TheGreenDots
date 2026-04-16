using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.API.Services;
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
