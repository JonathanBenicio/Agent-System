using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 1 — Gerencia ciclo de vida de chunks: aging, decay, promotion.
/// </summary>
public class ChunkLifecycleManager : IChunkLifecycleManager
{
    private readonly ConcurrentDictionary<string, ChunkLifecycle> _lifecycles = new();
    private readonly ILogger<ChunkLifecycleManager> _logger;

    public ChunkLifecycleManager(ILogger<ChunkLifecycleManager> logger)
    {
        _logger = logger;
    }

    public Task<ChunkLifecycle> GetLifecycleAsync(string chunkId)
    {
        var lifecycle = _lifecycles.GetOrAdd(chunkId, id => new ChunkLifecycle
        {
            ChunkId = id,
            State = ChunkLifecycleState.New,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            FreshnessScore = 1.0
        });

        return Task.FromResult(lifecycle);
    }

    public Task RecordAccessAsync(string chunkId)
    {
        if (_lifecycles.TryGetValue(chunkId, out var lifecycle))
        {
            lifecycle.LastAccessedAt = DateTime.UtcNow;
            lifecycle.AccessCount++;

            if (lifecycle.State == ChunkLifecycleState.New && lifecycle.AccessCount >= 3)
            {
                lifecycle.State = ChunkLifecycleState.Active;
                _logger.LogDebug("Chunk {ChunkId} promoted from New to Active (access count: {Count})", chunkId, lifecycle.AccessCount);
            }
        }

        return Task.CompletedTask;
    }

    public Task PromoteAsync(string chunkId)
    {
        if (_lifecycles.TryGetValue(chunkId, out var lifecycle))
        {
            lifecycle.State = lifecycle.State switch
            {
                ChunkLifecycleState.New => ChunkLifecycleState.Active,
                ChunkLifecycleState.Active => ChunkLifecycleState.Consolidated,
                _ => lifecycle.State
            };
            _logger.LogInformation("Chunk {ChunkId} promoted to {State}", chunkId, lifecycle.State);
        }

        return Task.CompletedTask;
    }

    public Task ArchiveAsync(string chunkId)
    {
        if (_lifecycles.TryGetValue(chunkId, out var lifecycle))
        {
            lifecycle.State = ChunkLifecycleState.Archived;
            lifecycle.ArchivedAt = DateTime.UtcNow;
            _logger.LogInformation("Chunk {ChunkId} archived", chunkId);
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<ChunkLifecycle>> GetStaleChunksAsync(TimeSpan threshold)
    {
        var cutoff = DateTime.UtcNow - threshold;
        var stale = _lifecycles.Values
            .Where(l => l.State != ChunkLifecycleState.Archived && l.LastAccessedAt < cutoff)
            .OrderBy(l => l.LastAccessedAt)
            .AsEnumerable();

        return Task.FromResult(stale);
    }

    public Task ConsolidateChunksAsync(IEnumerable<string> chunkIds, string targetChunkId)
    {
        foreach (var chunkId in chunkIds)
        {
            if (_lifecycles.TryGetValue(chunkId, out var lifecycle))
            {
                lifecycle.State = ChunkLifecycleState.Consolidated;
                lifecycle.ConsolidatedIntoId = targetChunkId;
            }
        }

        _lifecycles.GetOrAdd(targetChunkId, id => new ChunkLifecycle
        {
            ChunkId = id,
            State = ChunkLifecycleState.Active,
            FreshnessScore = 1.0
        });

        _logger.LogInformation("Consolidated {Count} chunks into {TargetId}", chunkIds.Count(), targetChunkId);
        return Task.CompletedTask;
    }

    public Task ApplyDecayAsync()
    {
        var decayed = 0;
        foreach (var lifecycle in _lifecycles.Values.Where(l => l.State != ChunkLifecycleState.Archived))
        {
            var age = DateTime.UtcNow - lifecycle.LastAccessedAt;
            var decayFactor = age.TotalHours * lifecycle.DecayRate;
            lifecycle.FreshnessScore = Math.Max(0.0, 1.0 - decayFactor);

            if (lifecycle.FreshnessScore < 0.1 && lifecycle.State == ChunkLifecycleState.Active)
            {
                lifecycle.State = ChunkLifecycleState.Archived;
                lifecycle.ArchivedAt = DateTime.UtcNow;
                decayed++;
            }
        }

        if (decayed > 0)
            _logger.LogInformation("Decay pass archived {Count} chunks", decayed);

        return Task.CompletedTask;
    }
}
