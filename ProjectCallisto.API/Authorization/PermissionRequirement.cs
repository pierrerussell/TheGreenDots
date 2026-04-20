using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;
using System.Security.Claims;

namespace ProjectCallisto.API.Authorization;

/// <summary>
/// Authorization requirement for a specific permission
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public Permission RequiredPermission { get; }

    public PermissionRequirement(Permission permission)
    {
        RequiredPermission = permission;
    }
}

/// <summary>
/// Handler that checks if user's role in the organisation has the required permission
/// </summary>
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _dbContext;

    public PermissionHandler(
        IHttpContextAccessor httpContextAccessor,
        AppDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Get subject ID from Auth0 "sub" claim (standard OIDC claim)
        var subjectId = context.User.FindFirst("sub")?.Value;
        if (subjectId == null)
        {
            context.Fail();
            return;
        }

        // Look up user by SubjectId
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.SubjectId == subjectId);

        if (user == null)
        {
            context.Fail();
            return;
        }

        // Get organisation ID from route parameter
        var httpContext = _httpContextAccessor.HttpContext;
        var orgIdValue = httpContext?.GetRouteValue("id")?.ToString();
        if (orgIdValue == null || !Guid.TryParse(orgIdValue, out var orgId))
        {
            context.Fail();
            return;
        }

        // Get user's role in this organisation
        var orgUser = await _dbContext.OrganisationUsers
            .FirstOrDefaultAsync(ou => ou.UserId == user.Id && ou.OrganisationId == orgId);

        if (orgUser == null)
        {
            context.Fail();
            return;
        }

        // Check if user's role has the required permission
        if (RolePermissions.HasPermission(orgUser.Role, requirement.RequiredPermission))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}
