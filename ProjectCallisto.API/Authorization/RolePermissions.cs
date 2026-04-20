using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.API.Authorization;

/// <summary>
/// Single source of truth for which roles have which permissions.
/// To add a new role: just add it to this dictionary with its permissions.
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<OrganisationRole, HashSet<Permission>> _rolePermissions = new()
    {
        [OrganisationRole.Admin] = new HashSet<Permission>
        {
            Permission.ViewDashboard,
            Permission.ManageSeats,
            Permission.ManageBilling,
            Permission.ExportData,
            Permission.InviteUsers,
            Permission.ManageSettings
        },

        [OrganisationRole.Member] = new HashSet<Permission>
        {
            Permission.ViewDashboard,
            Permission.ExportData
        }

        // Future roles go here:
        // [OrganisationRole.Manager] = new HashSet<Permission>
        // {
        //     Permission.ViewDashboard,
        //     Permission.ManageSeats,
        //     Permission.ExportData,
        //     Permission.InviteUsers,
        //     Permission.ManageSettings
        //     // But NOT ManageBilling
        // }
    };

    /// <summary>
    /// Check if a role has a specific permission
    /// </summary>
    public static bool HasPermission(OrganisationRole role, Permission permission)
    {
        return _rolePermissions.TryGetValue(role, out var permissions)
               && permissions.Contains(permission);
    }

    /// <summary>
    /// Check if a role has ALL of the specified permissions
    /// </summary>
    public static bool HasAllPermissions(OrganisationRole role, params Permission[] requiredPermissions)
    {
        if (!_rolePermissions.TryGetValue(role, out var permissions))
            return false;

        return requiredPermissions.All(p => permissions.Contains(p));
    }
}
