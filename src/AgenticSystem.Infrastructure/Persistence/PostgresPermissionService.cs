using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresPermissionService : IPermissionService
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;

    public PostgresPermissionService(IDbContextFactory<AgenticDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> HasPermissionAsync(string userId, string resource, Permission permission, CancellationToken ct = default)
    {
        var effectivePerms = await GetEffectivePermissionsAsync(userId, resource, ct);
        return (effectivePerms & permission) == permission;
    }

    public async Task<IReadOnlyList<RoleAssignment>> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        return await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(r => r.SubjectId == userId)
            .Select(r => new RoleAssignment { UserId = r.SubjectId, RoleName = r.Role, TenantId = r.TenantId, AssignedAt = r.GrantedAt })
            .ToListAsync(ct);
    }

    public async Task AssignRoleAsync(string userId, string role, string? tenantId = null, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await dbContext.RoleAssignments
            .FirstOrDefaultAsync(r => r.UserId == userId && r.RoleId == role && r.TenantId == tenantId, ct);

        if (existing is null)
        {
            dbContext.RoleAssignments.Add(new RoleAssignmentEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                RoleId = role,
                TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId)),
                GrantedAt = DateTime.UtcNow
            });
        }

        var membership = await dbContext.TenantMemberships.FirstOrDefaultAsync(
            item => item.SubjectId == userId && item.SubjectType == "User" && item.Role == role && item.TenantId == tenantId, ct);
        if (membership is null)
        {
            dbContext.TenantMemberships.Add(new TenantMembershipEntity
            {
                SubjectId = userId,
                SubjectType = "User",
                Role = role,
                TenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId)),
                GrantedAt = DateTime.UtcNow
            });
        }
        if (dbContext.ChangeTracker.HasChanges())
            await dbContext.SaveChangesAsync(ct);
    }

    public async Task RevokeRoleAsync(string userId, string role, string? tenantId = null, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await dbContext.RoleAssignments
            .FirstOrDefaultAsync(r => r.UserId == userId && r.RoleId == role && r.TenantId == tenantId, ct);

        if (existing is not null)
        {
            dbContext.RoleAssignments.Remove(existing);
        }

        var membership = await dbContext.TenantMemberships.FirstOrDefaultAsync(
            item => item.SubjectId == userId && item.SubjectType == "User" && item.Role == role && item.TenantId == tenantId, ct);
        if (membership is not null)
        {
            dbContext.TenantMemberships.Remove(membership);
        }
        if (dbContext.ChangeTracker.HasChanges())
            await dbContext.SaveChangesAsync(ct);
    }

    public async Task<Permission> GetEffectivePermissionsAsync(string userId, string resource, CancellationToken ct = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var roleNames = await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(r => r.SubjectId == userId)
            .Select(r => r.Role)
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
