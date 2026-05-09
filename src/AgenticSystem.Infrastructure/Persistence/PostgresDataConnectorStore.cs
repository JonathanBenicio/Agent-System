using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresDataConnectorStore : IDataConnectorStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresDataConnectorStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresDataConnectorStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresDataConnectorStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<DataConnectorConfig> SaveAsync(DataConnectorConfig config, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.DataConnectors.FindAsync(new object[] { config.Id }, ct);

        if (entity == null)
        {
            entity = new DataConnectorEntity { Id = config.Id };
            context.DataConnectors.Add(entity);
        }

        entity.Name = config.Name;
        entity.ConnectorType = config.ConnectorType.ToString();
        entity.ConnectionString = config.ConnectionString;
        entity.SettingsJson = JsonSerializer.Serialize(config.Settings, JsonOptions);
        entity.TenantId = config.TenantId;
        entity.SyncScheduleJson = JsonSerializer.Serialize(config.SyncSchedule, JsonOptions);
        entity.IsActive = config.IsActive;
        entity.LastSyncAt = config.LastSyncAt;
        entity.Status = config.Status.ToString();

        await context.SaveChangesAsync(ct);
        return config;
    }

    public async Task<DataConnectorConfig?> GetAsync(string id, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.DataConnectors.FindAsync(new object[] { id }, ct);
        return entity == null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<DataConnectorConfig>> ListAsync(string? tenantId = null, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = context.DataConnectors.AsQueryable();
        if (!string.IsNullOrEmpty(tenantId)) query = query.Where(c => c.TenantId == tenantId);

        var entities = await query.ToListAsync(ct);
        return entities.Select(Map).ToList();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.DataConnectors.FindAsync(new object[] { id }, ct);
        if (entity != null)
        {
            context.DataConnectors.Remove(entity);
            await context.SaveChangesAsync(ct);
        }
    }

    private static DataConnectorConfig Map(DataConnectorEntity entity)
    {
        return new DataConnectorConfig
        {
            Id = entity.Id,
            Name = entity.Name,
            ConnectorType = Enum.Parse<DataConnectorType>(entity.ConnectorType),
            ConnectionString = entity.ConnectionString,
            Settings = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.SettingsJson, JsonOptions) ?? new(),
            TenantId = entity.TenantId,
            SyncSchedule = JsonSerializer.Deserialize<DataSyncSchedule>(entity.SyncScheduleJson, JsonOptions) ?? new(),
            IsActive = entity.IsActive,
            LastSyncAt = entity.LastSyncAt,
            Status = Enum.Parse<ConnectorStatus>(entity.Status)
        };
    }
}
