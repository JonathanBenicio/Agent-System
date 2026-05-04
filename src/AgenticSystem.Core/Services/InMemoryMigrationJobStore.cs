using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Store in-memory de jobs de migração.
/// </summary>
public class InMemoryMigrationJobStore : IMigrationJobStore
{
    private readonly ConcurrentDictionary<string, EmbeddingMigrationJob> _jobs = new();

    public Task<EmbeddingMigrationJob?> GetAsync(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IEnumerable<EmbeddingMigrationJob>> GetAllAsync()
    {
        return Task.FromResult(_jobs.Values.AsEnumerable());
    }

    public Task SaveAsync(EmbeddingMigrationJob job)
    {
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string jobId)
    {
        _jobs.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }
}
