using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresPermissionService : IPermissionService
{
    private readonly AgenticDbContext _dbContext;

    public PostgresPermissionService(AgenticDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasPermissionAsync(string userId, string resource, Permission permission, CancellationToken ct = default)
    {
        var effectivePerms = await GetEffectivePermissionsAsync(userId, resource, ct);
        return (effectivePerms & permission) == permission;
    }

    public async Task<IReadOnlyList<RoleAssignment>> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        return await _dbContext.RoleAssignments
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => new RoleAssignment { UserId = r.UserId, RoleName = r.RoleId, TenantId = r.TenantId, AssignedAt = r.GrantedAt })
            .ToListAsync(ct);
    }

    public async Task AssignRoleAsync(string userId, string role, string? tenantId = null, CancellationToken ct = default)
    {
        var existing = await _dbContext.RoleAssignments
            .FirstOrDefaultAsync(r => r.UserId == userId && r.RoleId == role && r.TenantId == tenantId, ct);

        if (existing is null)
        {
            _dbContext.RoleAssignments.Add(new RoleAssignmentEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                RoleId = role,
                TenantId = tenantId,
                GrantedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeRoleAsync(string userId, string role, string? tenantId = null, CancellationToken ct = default)
    {
        var existing = await _dbContext.RoleAssignments
            .FirstOrDefaultAsync(r => r.UserId == userId && r.RoleId == role && r.TenantId == tenantId, ct);

        if (existing is not null)
        {
            _dbContext.RoleAssignments.Remove(existing);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task<Permission> GetEffectivePermissionsAsync(string userId, string resource, CancellationToken ct = default)
    {
        var roleNames = await _dbContext.RoleAssignments
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.RoleId)
            .ToListAsync(ct);

        Permission effective = Permission.None;

        foreach (var roleName in roleNames)
        {
            var builtIn = BuiltInRoles.All.FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
            if (builtIn != null)
            {
                effective |= builtIn.Permissions;
            }
            // In the future: Add lookup for custom roles in a RoleDefinitions table.
        }

        return effective;
    }
}
