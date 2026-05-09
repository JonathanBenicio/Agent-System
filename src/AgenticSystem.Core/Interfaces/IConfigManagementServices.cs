using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML22 — Gerenciador de configurações com encriptação e hot-reload.
/// </summary>
public interface IConfigManager
{
    Task<ConfigEntry> GetAsync(string key);
    Task<IEnumerable<ConfigEntry>> GetAllAsync(ConfigCategory? category = null);
    Task<ConfigEntry> SetAsync(ConfigEntryRequest request);
    Task<ConfigEntry> UpdateAsync(string key, ConfigEntryRequest request);
    Task DeleteAsync(string key);
    Task<ConfigValidationResult> ValidateAsync(string key);
    Task<IEnumerable<ConfigChangeLog>> GetAuditLogAsync(string? key = null, int limit = 50);
    Task<string?> ResolveValueAsync(string key);

    /// <summary>
    /// Rotates a secret by generating a new encrypted value and logging the rotation.
    /// </summary>
    Task<ConfigEntry> RotateSecretAsync(string key, string newValue);

    /// <summary>
    /// Returns all secrets that have expired or will expire within the given window.
    /// </summary>
    Task<IEnumerable<ConfigEntry>> GetExpiredSecretsAsync(TimeSpan? lookaheadWindow = null);
}

/// <summary>
/// Store de configurações (persistência).
/// </summary>
public interface IConfigStore
{
    Task<ConfigEntry?> GetByKeyAsync(string key);
    Task<IEnumerable<ConfigEntry>> GetAllAsync(ConfigCategory? category = null);
    Task SaveAsync(ConfigEntry entry);
    Task DeleteAsync(string key);
    Task<IEnumerable<ConfigChangeLog>> GetChangeLogsAsync(string? key = null, int limit = 50);
    Task SaveChangeLogAsync(ConfigChangeLog log);
}

/// <summary>
/// Serviço de encriptação para valores sensíveis.
/// </summary>
public interface IConfigEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    string Hash(string value);
}

/// <summary>
/// Provedor IOptionsMonitor-compatible para hot-reload de configurações do banco.
/// </summary>
public interface IConfigReloadNotifier
{
    void NotifyChange(string key);
    IDisposable OnChange(Action<string> listener);
}
