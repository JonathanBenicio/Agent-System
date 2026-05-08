using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// RBAC permission service — role-based access control for agents, tools, and resources.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if a user has a specific permission on a resource.
    /// </summary>
    Task<bool> HasPermissionAsync(string userId, string resource, Permission permission, CancellationToken ct = default);

    /// <summary>
    /// Gets all role assignments for a user.
    /// </summary>
    Task<IReadOnlyList<RoleAssignment>> GetRolesAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Assigns a role to a user, optionally scoped to a tenant.
    /// </summary>
    Task AssignRoleAsync(string userId, string role, string? tenantId = null, CancellationToken ct = default);

    /// <summary>
    /// Revokes a role from a user, optionally scoped to a tenant.
    /// </summary>
    Task RevokeRoleAsync(string userId, string role, string? tenantId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the effective permissions for a user on a resource.
    /// </summary>
    Task<Permission> GetEffectivePermissionsAsync(string userId, string resource, CancellationToken ct = default);
}
