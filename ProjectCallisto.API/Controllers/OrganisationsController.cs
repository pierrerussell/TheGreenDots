using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.Controllers;

[Authorize]
[ApiController]
[Route("api/organisations")]
public class OrganisationsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public OrganisationsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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

    private async Task<User?> GetCurrentUserAsync()
    {
        var subjectId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (subjectId == null) return null;
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId);
    }
}
