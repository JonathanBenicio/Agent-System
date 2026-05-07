using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresEmbeddingModelStore : IEmbeddingModelStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresEmbeddingModelStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresEmbeddingModelStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresEmbeddingModelStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<EmbeddingModelConfig?> GetAsync(string modelId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var json = await db.EmbeddingModels
            .AsNoTracking()
            .Where(item => item.Id == modelId)
            .Select(item => item.DataJson)
            .FirstOrDefaultAsync();

        return json is null ? null : JsonSerializer.Deserialize<EmbeddingModelConfig>(json, JsonOptions);
    }

    public async Task<IEnumerable<EmbeddingModelConfig>> GetAllAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var payloads = await db.EmbeddingModels
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.DataJson)
            .ToListAsync();

        return payloads
            .Select(payload =>
            {
                try
                {
                    return JsonSerializer.Deserialize<EmbeddingModelConfig>(payload, JsonOptions);
                }
                catch (JsonException)
                {
                    return null;
                }
            })
            .OfType<EmbeddingModelConfig>()
            .ToList();
    }

    public async Task<EmbeddingModelConfig> GetActiveAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var json = await db.EmbeddingModels
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.DataJson)
            .FirstOrDefaultAsync();

        if (json is null)
        {
            throw new InvalidOperationException("No active embedding model configured.");
        }

        var model = JsonSerializer.Deserialize<EmbeddingModelConfig>(json, JsonOptions);
        if (model is null)
        {
            throw new InvalidOperationException("Active embedding model payload is invalid.");
        }

        return model;
    }

    public async Task SaveAsync(EmbeddingModelConfig model)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.EmbeddingModels.FirstOrDefaultAsync(item => item.Id == model.Id);
        if (entity is null)
        {
            db.EmbeddingModels.Add(new EmbeddingModelEntity
            {
                Id = model.Id,
                Name = model.Name,
                IsActive = model.IsActive,
                DataJson = JsonSerializer.Serialize(model, JsonOptions),
                CreatedAt = model.CreatedAt
            });
        }
        else
        {
            entity.Name = model.Name;
            entity.IsActive = model.IsActive;
            entity.DataJson = JsonSerializer.Serialize(model, JsonOptions);
        }

        await db.SaveChangesAsync();
        _logger.LogDebug("Embedding model saved to PostgreSQL via EF Core: {ModelId}", model.Id);
    }

    public async Task DeleteAsync(string modelId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.EmbeddingModels.FirstOrDefaultAsync(item => item.Id == modelId);
        if (entity is null)
        {
            return;
        }

        db.EmbeddingModels.Remove(entity);
        await db.SaveChangesAsync();
        _logger.LogDebug("Embedding model deleted from PostgreSQL via EF Core: {ModelId}", modelId);
    }

    public async Task SetActiveAsync(string modelId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var models = await db.EmbeddingModels.ToListAsync();
        var target = models.FirstOrDefault(item => item.Id == modelId);
        if (target is null)
        {
            throw new InvalidOperationException($"Embedding model '{modelId}' not found.");
        }

        foreach (var model in models)
        {
            var wasActive = model.IsActive;
            model.IsActive = model.Id == modelId;

            if (wasActive != model.IsActive)
            {
                var payload = JsonSerializer.Deserialize<EmbeddingModelConfig>(model.DataJson, JsonOptions);
                if (payload is not null)
                {
                    payload.IsActive = model.IsActive;
                    model.DataJson = JsonSerializer.Serialize(payload, JsonOptions);
                }
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Active embedding model set to: {ModelId}", modelId);
    }
}
