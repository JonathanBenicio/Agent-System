using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Store in-memory de modelos de embedding.
/// </summary>
public class InMemoryEmbeddingModelStore : IEmbeddingModelStore
{
    private readonly ConcurrentDictionary<string, EmbeddingModelConfig> _models = new();
    private string _activeModelId = string.Empty;

    public Task<EmbeddingModelConfig?> GetAsync(string modelId)
    {
        _models.TryGetValue(modelId, out var model);
        return Task.FromResult(model);
    }

    public Task<IEnumerable<EmbeddingModelConfig>> GetAllAsync()
    {
        return Task.FromResult(_models.Values.AsEnumerable());
    }

    public Task<EmbeddingModelConfig> GetActiveAsync()
    {
        if (!string.IsNullOrEmpty(_activeModelId) && _models.TryGetValue(_activeModelId, out var active))
            return Task.FromResult(active);

        var first = _models.Values.FirstOrDefault(m => m.IsActive)
            ?? _models.Values.FirstOrDefault()
            ?? throw new InvalidOperationException("No embedding model configured.");
        return Task.FromResult(first);
    }

    public Task SaveAsync(EmbeddingModelConfig model)
    {
        _models[model.Id] = model;
        if (model.IsActive)
            _activeModelId = model.Id;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string modelId)
    {
        _models.TryRemove(modelId, out _);
        if (_activeModelId == modelId)
            _activeModelId = string.Empty;
        return Task.CompletedTask;
    }

    public Task SetActiveAsync(string modelId)
    {
        if (!_models.ContainsKey(modelId))
            throw new KeyNotFoundException($"Model '{modelId}' not found.");

        foreach (var m in _models.Values)
            m.IsActive = m.Id == modelId;

        _activeModelId = modelId;
        return Task.CompletedTask;
    }
}
