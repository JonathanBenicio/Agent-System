using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public sealed class InMemoryChatSettingsStore : IChatSettingsStore
{
    private readonly ConcurrentDictionary<(string Tenant, string User), ChatSettings> _settings = new();

    public Task<ChatSettings?> GetAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_settings.GetValueOrDefault((tenantId, userId)));
    }

    public Task SaveAsync(ChatSettings settings, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _settings[(settings.TenantId, settings.UserId)] = settings;
        return Task.CompletedTask;
    }
}
