using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresConfigStore : IConfigStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresConfigStore> _logger;

    public PostgresConfigStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresConfigStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<ConfigEntry?> GetByKeyAsync(string key)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.ConfigEntries.AsNoTracking().FirstOrDefaultAsync(item => item.Key == key);
        return entity is null ? null : MapEntry(entity);
    }

    public async Task<IEnumerable<ConfigEntry>> GetAllAsync(ConfigCategory? category = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var query = db.ConfigEntries.AsNoTracking().AsQueryable();
        if (category is not null)
        {
            query = query.Where(item => item.Category == category.Value.ToString());
        }

        var entities = await query.OrderBy(item => item.Key).ToListAsync();
        return entities.Select(MapEntry).ToList();
    }

    public async Task SaveAsync(ConfigEntry entry)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.ConfigEntries.FirstOrDefaultAsync(item => item.Key == entry.Key);
        if (entity is null)
        {
            db.ConfigEntries.Add(MapEntity(entry));
        }
        else
        {
            entity.Value = entry.Value;
            entity.EncryptedValue = entry.EncryptedValue;
            entity.IsSecret = entry.IsSecret;
            entity.Category = entry.Category.ToString();
            entity.Status = entry.Status.ToString();
            entity.Description = entry.Description;
            entity.Provider = entry.Provider;
            entity.UpdatedAt = entry.UpdatedAt;
            entity.ExpiresAt = entry.ExpiresAt;
            entity.MetadataJson = JsonSerializer.Serialize(entry.Metadata);
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string key)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.ConfigEntries.FirstOrDefaultAsync(item => item.Key == key);
        if (entity is null)
        {
            return;
        }

        db.ConfigEntries.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<ConfigChangeLog>> GetChangeLogsAsync(string? key = null, int limit = 50)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var query = db.ConfigChangeLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(key))
        {
            query = query.Where(item => item.ConfigKey == key);
        }

        var logs = await query
            .OrderByDescending(item => item.ChangedAt)
            .Take(limit)
            .ToListAsync();

        return logs.Select(item => new ConfigChangeLog
        {
            Id = item.Id,
            ConfigKey = item.ConfigKey,
            Action = item.Action,
            ChangedBy = item.ChangedBy,
            ChangedAt = item.ChangedAt,
            PreviousValueHash = item.PreviousValueHash,
            NewValueHash = item.NewValueHash
        }).ToList();
    }

    public async Task SaveChangeLogAsync(ConfigChangeLog log)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        db.ConfigChangeLogs.Add(new ConfigChangeLogEntity
        {
            Id = log.Id,
            ConfigKey = log.ConfigKey,
            Action = log.Action,
            ChangedBy = log.ChangedBy,
            ChangedAt = log.ChangedAt,
            PreviousValueHash = log.PreviousValueHash,
            NewValueHash = log.NewValueHash
        });

        await db.SaveChangesAsync();
    }

    private ConfigEntry MapEntry(ConfigEntryEntity entity)
    {
        return new ConfigEntry
        {
            Id = entity.Id,
            Key = entity.Key,
            Value = entity.Value,
            EncryptedValue = entity.EncryptedValue,
            IsSecret = entity.IsSecret,
            Category = Enum.TryParse<ConfigCategory>(entity.Category, out var category) ? category : ConfigCategory.General,
            Status = Enum.TryParse<ConfigEntryStatus>(entity.Status, out var status) ? status : ConfigEntryStatus.Active,
            Description = entity.Description,
            Provider = entity.Provider,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ExpiresAt = entity.ExpiresAt,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson) ?? new Dictionary<string, string>()
        };
    }

    private static ConfigEntryEntity MapEntity(ConfigEntry entry)
    {
        return new ConfigEntryEntity
        {
            Id = entry.Id,
            Key = entry.Key,
            Value = entry.Value,
            EncryptedValue = entry.EncryptedValue,
            IsSecret = entry.IsSecret,
            Category = entry.Category.ToString(),
            Status = entry.Status.ToString(),
            Description = entry.Description,
            Provider = entry.Provider,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            ExpiresAt = entry.ExpiresAt,
            MetadataJson = JsonSerializer.Serialize(entry.Metadata)
        };
    }
}
