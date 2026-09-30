using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

public sealed class InMemoryPlatformConfigStore : IPlatformConfigStore
{
    private readonly ConcurrentDictionary<string, (string Value, bool IsSecret)> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConfigReloadNotifier? _reloadNotifier;
    private readonly IConfigEncryptionService? _encryption;

    public InMemoryPlatformConfigStore(
        IConfigReloadNotifier? reloadNotifier = null,
        IConfigEncryptionService? encryption = null)
    {
        _reloadNotifier = reloadNotifier;
        _encryption = encryption;
    }

    public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_values.TryGetValue(key, out var entry))
            return Task.FromResult<string?>(null);

        var value = entry.IsSecret && _encryption is not null
            ? _encryption.Decrypt(entry.Value)
            : entry.Value;
        return Task.FromResult<string?>(value);
    }

    public Task SetValuesAsync(
        IReadOnlyCollection<PlatformConfigValue> values,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var entry in values)
        {
            _values[entry.Key] = (entry.IsSecret && _encryption is not null ? _encryption.Encrypt(entry.Value) : entry.Value, entry.IsSecret);
            _reloadNotifier?.NotifyChange(entry.Key);
        }
        return Task.CompletedTask;
    }

    public Task SetValueAsync(
        string key,
        string value,
        bool isSecret,
        string changedBy,
        CancellationToken cancellationToken = default) =>
        SetValuesAsync([new PlatformConfigValue(key, value, isSecret)], changedBy, cancellationToken);
}
