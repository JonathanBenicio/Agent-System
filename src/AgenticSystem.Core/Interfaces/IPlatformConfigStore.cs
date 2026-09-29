namespace AgenticSystem.Core.Interfaces;

/// <summary>Persists platform-wide settings independently from tenant configuration.</summary>
public interface IPlatformConfigStore
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    Task SetValuesAsync(
        IReadOnlyCollection<PlatformConfigValue> values,
        string changedBy,
        CancellationToken cancellationToken = default);

    Task SetValueAsync(
        string key,
        string value,
        bool isSecret,
        string changedBy,
        CancellationToken cancellationToken = default) =>
        SetValuesAsync([new PlatformConfigValue(key, value, isSecret)], changedBy, cancellationToken);
}

public sealed record PlatformConfigValue(string Key, string Value, bool IsSecret = false);
