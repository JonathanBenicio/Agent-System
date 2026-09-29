using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresChatSettingsStore(IDbContextFactory<AgenticDbContext> dbContextFactory) : IChatSettingsStore
{
    public async Task<ChatSettings?> GetAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ChatSettings.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, ct);
        return entity is null ? null : new ChatSettings(entity.TenantId, entity.UserId, entity.Provider, entity.Model, entity.UpdatedAt);
    }

    public async Task SaveAsync(ChatSettings settings, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ChatSettings
            .FirstOrDefaultAsync(item => item.TenantId == settings.TenantId && item.UserId == settings.UserId, ct);
        if (entity is null)
        {
            db.ChatSettings.Add(new ChatSettingsEntity
            {
                TenantId = settings.TenantId, UserId = settings.UserId,
                Provider = settings.Provider, Model = settings.Model, UpdatedAt = settings.UpdatedAt
            });
        }
        else
        {
            entity.Provider = settings.Provider;
            entity.Model = settings.Model;
            entity.UpdatedAt = settings.UpdatedAt;
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            var current = await db.ChatSettings
                .FirstAsync(item => item.TenantId == settings.TenantId && item.UserId == settings.UserId, ct);
            current.Provider = settings.Provider;
            current.Model = settings.Model;
            current.UpdatedAt = settings.UpdatedAt;
            await db.SaveChangesAsync(ct);
        }
    }
}
