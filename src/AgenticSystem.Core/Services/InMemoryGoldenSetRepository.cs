using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryGoldenSetRepository : IGoldenSetRepository
{
    private static readonly ConcurrentDictionary<string, GoldenSet> _store = new();

    private static GoldenSet Clone(GoldenSet model)
    {
        return new GoldenSet
        {
            Id = model.Id,
            TenantId = model.TenantId,
            Name = model.Name,
            Description = model.Description,
            AgentName = model.AgentName,
            Cases = model.Cases?.Select(c => new GoldenSetCase
            {
                Id = c.Id,
                Input = c.Input,
                ExpectedOutput = c.ExpectedOutput,
                Tags = c.Tags != null ? new List<string>(c.Tags) : new List<string>()
            }).ToList() ?? new List<GoldenSetCase>(),
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }

    public Task<GoldenSet?> GetByIdAsync(string id, string tenantId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(id, out var model) && model.TenantId == tenantId)
        {
            return Task.FromResult<GoldenSet?>(Clone(model));
        }
        return Task.FromResult<GoldenSet?>(null);
    }

    public Task<IReadOnlyList<GoldenSet>> ListAsync(string tenantId, string? agentName, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _store.Values.Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(agentName))
        {
            query = query.Where(x => x.AgentName == agentName);
        }

        IReadOnlyList<GoldenSet> result = query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Clone)
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddAsync(GoldenSet model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
        {
            model.Id = Guid.NewGuid().ToString("N");
        }

        _store[model.Id] = Clone(model);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(GoldenSet model, CancellationToken ct = default)
    {
        model.UpdatedAt = DateTime.UtcNow;
        _store[model.Id] = Clone(model);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, string tenantId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(id, out var model) && model.TenantId == tenantId)
        {
            _store.TryRemove(id, out _);
        }
        return Task.CompletedTask;
    }
}
