using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresMigrationJobStore : IMigrationJobStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresMigrationJobStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresMigrationJobStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresMigrationJobStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<EmbeddingMigrationJob?> GetAsync(string jobId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var json = await db.MigrationJobs
            .AsNoTracking()
            .Where(item => item.Id == jobId)
            .Select(item => item.DataJson)
            .FirstOrDefaultAsync();

        return json is null ? null : JsonSerializer.Deserialize<EmbeddingMigrationJob>(json, JsonOptions);
    }

    public async Task<IEnumerable<EmbeddingMigrationJob>> GetAllAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var payloads = await db.MigrationJobs
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.DataJson)
            .ToListAsync();

        return payloads
            .Select(payload =>
            {
                try
                {
                    return JsonSerializer.Deserialize<EmbeddingMigrationJob>(payload, JsonOptions);
                }
                catch (JsonException)
                {
                    return null;
                }
            })
            .OfType<EmbeddingMigrationJob>()
            .ToList();
    }

    public async Task SaveAsync(EmbeddingMigrationJob job)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.MigrationJobs.FirstOrDefaultAsync(item => item.Id == job.Id);
        if (entity is null)
        {
            db.MigrationJobs.Add(new MigrationJobEntity
            {
                Id = job.Id,
                Status = job.Status.ToString(),
                DataJson = JsonSerializer.Serialize(job, JsonOptions),
                CreatedAt = job.CreatedAt
            });
        }
        else
        {
            entity.Status = job.Status.ToString();
            entity.DataJson = JsonSerializer.Serialize(job, JsonOptions);
        }

        await db.SaveChangesAsync();
        _logger.LogDebug("Migration job saved to PostgreSQL via EF Core: {JobId}", job.Id);
    }

    public async Task DeleteAsync(string jobId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.MigrationJobs.FirstOrDefaultAsync(item => item.Id == jobId);
        if (entity is null)
        {
            return;
        }

        db.MigrationJobs.Remove(entity);
        await db.SaveChangesAsync();
        _logger.LogDebug("Migration job deleted from PostgreSQL via EF Core: {JobId}", jobId);
    }
}
