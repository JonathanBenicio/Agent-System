using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// In-memory RBAC permission service with built-in roles and wildcard resource matching.
/// </summary>
public class InMemoryPermissionService : IPermissionService
{
    private readonly ConcurrentDictionary<string, List<RoleAssignment>> _assignments = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RoleDefinition> _roles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<InMemoryPermissionService> _logger;

    public InMemoryPermissionService(ILogger<InMemoryPermissionService> logger)
    {
        _logger = logger;

        // Register built-in roles
        foreach (var role in BuiltInRoles.All)
        {
            _roles[role.Name] = role;
        }
    }

    public Task<bool> HasPermissionAsync(string userId, string resource, Permission permission, CancellationToken ct = default)
    {
        var effective = GetEffectivePermissionsInternal(userId, resource);
        var has = (effective & permission) == permission;
        return Task.FromResult(has);
    }

    public Task<IReadOnlyList<RoleAssignment>> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        if (!_assignments.TryGetValue(userId, out var assignments))
        {
            return Task.FromResult<IReadOnlyList<RoleAssignment>>(Array.Empty<RoleAssignment>());
        }

        lock (assignments)
        {
            var active = assignments.Where(a => a.IsActive).ToList();
            return Task.FromResult<IReadOnlyList<RoleAssignment>>(active);
        }
    }

    public Task AssignRoleAsync(string userId, string role, string? tenantId = null, CancellationToken ct = default)
    {
        if (!_roles.ContainsKey(role))
        {
            throw new ArgumentException($"Role '{role}' does not exist.", nameof(role));
        }

        var assignments = _assignments.GetOrAdd(userId, _ => new List<RoleAssignment>());
        lock (assignments)
        {
            // Avoid duplicate assignment
            var exists = assignments.Any(a =>
                a.IsActive &&
                a.RoleName.Equals(role, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(a.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                assignments.Add(new RoleAssignment
                {
                    UserId = userId,
                    RoleName = role,
                    TenantId = tenantId
                });
                _logger.LogInformation("Role '{Role}' assigned to user '{UserId}' (tenant: {TenantId})", role, userId, tenantId ?? "global");
            }
        }

        return Task.CompletedTask;
    }

    public Task RevokeRoleAsync(string userId, string role, string? tenantId = null, CancellationToken ct = default)
    {
        if (!_assignments.TryGetValue(userId, out var assignments))
        {
            return Task.CompletedTask;
        }

        lock (assignments)
        {
            var toRemove = assignments
                .Where(a => a.RoleName.Equals(role, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(a.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var assignment in toRemove)
            {
                assignments.Remove(assignment);
            }

            if (toRemove.Count > 0)
            {
                _logger.LogInformation("Role '{Role}' revoked from user '{UserId}' (tenant: {TenantId})", role, userId, tenantId ?? "global");
            }
        }

        return Task.CompletedTask;
    }

    public Task<Permission> GetEffectivePermissionsAsync(string userId, string resource, CancellationToken ct = default)
    {
        return Task.FromResult(GetEffectivePermissionsInternal(userId, resource));
    }

    private Permission GetEffectivePermissionsInternal(string userId, string resource)
    {
        if (!_assignments.TryGetValue(userId, out var assignments))
        {
            return Permission.None;
        }

        var effective = Permission.None;

        lock (assignments)
        {
            foreach (var assignment in assignments.Where(a => a.IsActive))
            {
                if (!_roles.TryGetValue(assignment.RoleName, out var roleDef))
                    continue;

                // If role has no scopes, it applies to all resources
                if (roleDef.ResourceScopes.Count == 0)
                {
                    effective |= roleDef.Permissions;
                    continue;
                }

                // Check if resource matches any scope
                if (roleDef.ResourceScopes.Any(scope => MatchesScope(resource, scope)))
                {
                    effective |= roleDef.Permissions;
                }
            }
        }

        return effective;
    }

    private static bool MatchesScope(string resource, string scope)
    {
        if (scope == "*") return true;
        if (scope.EndsWith("/*"))
        {
            var prefix = scope[..^2];
            return resource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return resource.Equals(scope, StringComparison.OrdinalIgnoreCase);
    }
}
