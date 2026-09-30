using System.Security.Cryptography;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresPlatformConfigStore : IPlatformConfigStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly IConfigEncryptionService _encryption;
    private readonly IConfigReloadNotifier _reloadNotifier;
    private readonly ILogger<PostgresPlatformConfigStore> _logger;

    public PostgresPlatformConfigStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        IConfigEncryptionService encryption,
        IConfigReloadNotifier reloadNotifier,
        ILogger<PostgresPlatformConfigStore> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
        _reloadNotifier = reloadNotifier ?? throw new ArgumentNullException(nameof(reloadNotifier));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.PlatformConfigs.AsNoTracking()
            .SingleOrDefaultAsync(setting => setting.Key == key, cancellationToken);
        if (entity is null)
            return null;

        return entity.IsSecret && entity.EncryptedValue is not null
            ? _encryption.Decrypt(entity.EncryptedValue)
            : entity.Value;
    }

    public Task SetValueAsync(
        string key,
        string value,
        bool isSecret,
        string changedBy,
        CancellationToken cancellationToken = default) =>
        SetValuesAsync([new PlatformConfigValue(key, value, isSecret)], changedBy, cancellationToken);

    public async Task SetValuesAsync(
        IReadOnlyCollection<PlatformConfigValue> values,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);
        if (values.Count == 0)
            return;
        if (values.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null))
            throw new ArgumentException("Platform configuration keys and values are required.", nameof(values));

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var entry in values)
        {
            var entity = await db.PlatformConfigs.SingleOrDefaultAsync(setting => setting.Key == entry.Key, cancellationToken);
            var previousValue = entity is null
                ? null
                : entity.IsSecret && entity.EncryptedValue is not null
                    ? _encryption.Decrypt(entity.EncryptedValue)
                    : entity.Value;

            if (entity is null)
            {
                entity = new PlatformConfigEntity { Key = entry.Key };
                db.PlatformConfigs.Add(entity);
            }

            entity.Value = entry.IsSecret ? "********" : entry.Value;
            entity.EncryptedValue = entry.IsSecret ? _encryption.Encrypt(entry.Value) : null;
            entity.IsSecret = entry.IsSecret;
            entity.ChangedBy = changedBy;
            entity.UpdatedAt = now;

            db.PlatformConfigAudits.Add(new PlatformConfigAuditEntity
            {
                Key = entry.Key,
                Action = previousValue is null ? "Created" : "Updated",
                ChangedBy = changedBy,
                PreviousValueHash = previousValue is null ? null : Hash(previousValue),
                NewValueHash = Hash(entry.Value),
                ChangedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var entry in values)
            _reloadNotifier.NotifyChange(entry.Key);
        if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            try
            {
                foreach (var entry in values)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "SELECT pg_notify('config_changed', {0})",
                        new object[] { entry.Key },
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Persisted platform config, but cross-node reload notification failed");
            }
        }
    }

    private string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
