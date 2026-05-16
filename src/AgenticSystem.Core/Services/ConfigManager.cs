using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML22 — Gerenciador de configurações com encriptação, audit trail e hot-reload.
/// </summary>
public class ConfigManager : IConfigManager
{
    private readonly IConfigStore _store;
    private readonly IConfigEncryptionService _encryption;
    private readonly IConfigReloadNotifier _reloadNotifier;
    private readonly ILogger<ConfigManager> _logger;

    public ConfigManager(
        IConfigStore store,
        IConfigEncryptionService encryption,
        IConfigReloadNotifier reloadNotifier,
        ILogger<ConfigManager> logger)
    {
        _store = store;
        _encryption = encryption;
        _reloadNotifier = reloadNotifier;
        _logger = logger;
    }

    public async Task<ConfigEntry> GetAsync(string key)
    {
        var entry = await _store.GetByKeyAsync(key)
            ?? throw new KeyNotFoundException($"Configuration key '{key}' not found.");

        // Retorna sem valor desencriptado se for secret (frontend nunca vê plaintext)
        if (entry.IsSecret)
        {
            return new ConfigEntry
            {
                Id = entry.Id,
                Key = entry.Key,
                Value = "********",
                IsSecret = true,
                Category = entry.Category,
                Status = entry.Status,
                Description = entry.Description,
                Provider = entry.Provider,
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt,
                ExpiresAt = entry.ExpiresAt,
                Metadata = entry.Metadata
            };
        }

        return entry;
    }

    public async Task<IEnumerable<ConfigEntry>> GetAllAsync(ConfigCategory? category = null)
    {
        var entries = await _store.GetAllAsync(category);
        return entries.Select(e => e.IsSecret
            ? new ConfigEntry
            {
                Id = e.Id,
                Key = e.Key,
                Value = "********",
                IsSecret = true,
                Category = e.Category,
                Status = e.Status,
                Description = e.Description,
                Provider = e.Provider,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                ExpiresAt = e.ExpiresAt,
                Metadata = e.Metadata
            }
            : e);
    }

    public async Task<ConfigEntry> SetAsync(ConfigEntryRequest request)
    {
        var entry = new ConfigEntry
        {
            Key = request.Key,
            Value = request.IsSecret ? "********" : request.Value,
            EncryptedValue = request.IsSecret ? _encryption.Encrypt(request.Value) : null,
            IsSecret = request.IsSecret,
            Category = request.Category,
            Description = request.Description,
            Provider = request.Provider,
            ExpiresAt = request.ExpiresAt,
            Status = ConfigEntryStatus.Active
        };

        await _store.SaveAsync(entry);

        await _store.SaveChangeLogAsync(new ConfigChangeLog
        {
            ConfigKey = entry.Key,
            Action = "Created",
            NewValueHash = _encryption.Hash(request.Value)
        });

        await _store.NotifyChangeAsync(entry.Key);
        _logger.LogInformation("Configuration '{Key}' created (category: {Category})", entry.Key, entry.Category);

        return entry;
    }

    public async Task<ConfigEntry> UpdateAsync(string key, ConfigEntryRequest request)
    {
        var existing = await _store.GetByKeyAsync(key)
            ?? throw new KeyNotFoundException($"Configuration key '{key}' not found.");

        var previousHash = existing.IsSecret && existing.EncryptedValue != null
            ? _encryption.Hash(_encryption.Decrypt(existing.EncryptedValue))
            : _encryption.Hash(existing.Value);

        existing.Value = request.IsSecret ? "********" : request.Value;
        existing.EncryptedValue = request.IsSecret ? _encryption.Encrypt(request.Value) : null;
        existing.IsSecret = request.IsSecret;
        existing.Category = request.Category;
        existing.Description = request.Description;
        existing.Provider = request.Provider;
        existing.ExpiresAt = request.ExpiresAt;
        existing.UpdatedAt = DateTime.UtcNow;

        await _store.SaveAsync(existing);

        await _store.SaveChangeLogAsync(new ConfigChangeLog
        {
            ConfigKey = key,
            Action = "Updated",
            PreviousValueHash = previousHash,
            NewValueHash = _encryption.Hash(request.Value)
        });

        await _store.NotifyChangeAsync(key);
        _logger.LogInformation("Configuration '{Key}' updated", key);

        return existing;
    }

    public async Task DeleteAsync(string key)
    {
        var existing = await _store.GetByKeyAsync(key)
            ?? throw new KeyNotFoundException($"Configuration key '{key}' not found.");

        await _store.DeleteAsync(key);

        await _store.SaveChangeLogAsync(new ConfigChangeLog
        {
            ConfigKey = key,
            Action = "Deleted"
        });

        await _store.NotifyChangeAsync(key);
        _logger.LogInformation("Configuration '{Key}' deleted", key);
    }

    public async Task<ConfigValidationResult> ValidateAsync(string key)
    {
        var entry = await _store.GetByKeyAsync(key);
        if (entry == null)
        {
            return new ConfigValidationResult
            {
                IsValid = false,
                Key = key,
                ErrorMessage = "Key not found"
            };
        }

        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        {
            return new ConfigValidationResult
            {
                IsValid = false,
                Key = key,
                ErrorMessage = "Configuration has expired"
            };
        }

        if (entry.IsSecret && string.IsNullOrEmpty(entry.EncryptedValue))
        {
            return new ConfigValidationResult
            {
                IsValid = false,
                Key = key,
                ErrorMessage = "Secret value is missing"
            };
        }

        return new ConfigValidationResult { IsValid = true, Key = key };
    }

    public async Task<IEnumerable<ConfigChangeLog>> GetAuditLogAsync(string? key = null, int limit = 50)
    {
        return await _store.GetChangeLogsAsync(key, limit);
    }

    /// <summary>
    /// Resolve o valor real de uma config (desencripta se necessário).
    /// Uso interno — não expor via API.
    /// </summary>
    public async Task<string?> ResolveValueAsync(string key)
    {
        var entry = await _store.GetByKeyAsync(key);
        if (entry == null) return null;

        return entry.IsSecret && entry.EncryptedValue != null
            ? _encryption.Decrypt(entry.EncryptedValue)
            : entry.Value;
    }

    public async Task<ConfigEntry> RotateSecretAsync(string key, string newValue)
    {
        var existing = await _store.GetByKeyAsync(key)
            ?? throw new KeyNotFoundException($"Configuration key '{key}' not found.");

        if (!existing.IsSecret)
        {
            throw new InvalidOperationException($"Configuration '{key}' is not a secret and cannot be rotated.");
        }

        var previousHash = existing.EncryptedValue != null
            ? _encryption.Hash(_encryption.Decrypt(existing.EncryptedValue))
            : null;

        existing.EncryptedValue = _encryption.Encrypt(newValue);
        existing.Value = "********";
        existing.UpdatedAt = DateTime.UtcNow;

        await _store.SaveAsync(existing);

        await _store.SaveChangeLogAsync(new ConfigChangeLog
        {
            ConfigKey = key,
            Action = "Rotated",
            PreviousValueHash = previousHash,
            NewValueHash = _encryption.Hash(newValue)
        });

        await _store.NotifyChangeAsync(key);
        _logger.LogInformation("Secret '{Key}' rotated successfully", key);

        return new ConfigEntry
        {
            Id = existing.Id,
            Key = existing.Key,
            Value = "********",
            IsSecret = true,
            Category = existing.Category,
            Status = existing.Status,
            Description = existing.Description,
            Provider = existing.Provider,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = existing.UpdatedAt,
            ExpiresAt = existing.ExpiresAt,
            Metadata = existing.Metadata
        };
    }

    public async Task<IEnumerable<ConfigEntry>> GetExpiredSecretsAsync(TimeSpan? lookaheadWindow = null)
    {
        var window = lookaheadWindow ?? TimeSpan.FromDays(7);
        var threshold = DateTime.UtcNow.Add(window);

        var allEntries = await _store.GetAllAsync();
        return allEntries
            .Where(e => e.IsSecret && e.ExpiresAt.HasValue && e.ExpiresAt.Value <= threshold)
            .OrderBy(e => e.ExpiresAt)
            .Select(e => new ConfigEntry
            {
                Id = e.Id,
                Key = e.Key,
                Value = "********",
                IsSecret = true,
                Category = e.Category,
                Status = e.Status,
                Description = e.Description,
                Provider = e.Provider,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                ExpiresAt = e.ExpiresAt,
                Metadata = e.Metadata
            });
    }
}
