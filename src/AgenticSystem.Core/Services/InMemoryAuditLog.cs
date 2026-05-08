using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// In-memory append-only audit log. Entries are immutable once recorded.
/// </summary>
public class InMemoryAuditLog : IAuditLog
{
    private readonly ConcurrentBag<AuditEntry> _entries = new();
    private readonly ILogger<InMemoryAuditLog> _logger;

    public InMemoryAuditLog(ILogger<InMemoryAuditLog> logger)
    {
        _logger = logger;
    }

    public Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        _entries.Add(entry);

        if (!entry.Success)
        {
            _logger.LogWarning("Audit [{Category}] FAILED: {Action} by {UserId} — {Error}",
                entry.Category, entry.Action, entry.UserId ?? "system", entry.ErrorMessage);
        }
        else
        {
            _logger.LogDebug("Audit [{Category}]: {Action} by {UserId}",
                entry.Category, entry.Action, entry.UserId ?? "system");
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        var results = ApplyFilters(_entries, query)
            .OrderByDescending(e => e.Timestamp)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditEntry>>(results);
    }

    public Task<long> CountAsync(AuditQuery query, CancellationToken ct = default)
    {
        var count = ApplyFilters(_entries, query).LongCount();
        return Task.FromResult(count);
    }

    private static IEnumerable<AuditEntry> ApplyFilters(IEnumerable<AuditEntry> entries, AuditQuery query)
    {
        if (query.Category.HasValue)
            entries = entries.Where(e => e.Category == query.Category.Value);

        if (!string.IsNullOrWhiteSpace(query.UserId))
            entries = entries.Where(e => string.Equals(e.UserId, query.UserId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.TenantId))
            entries = entries.Where(e => string.Equals(e.TenantId, query.TenantId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.SessionId))
            entries = entries.Where(e => string.Equals(e.SessionId, query.SessionId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.AgentName))
            entries = entries.Where(e => string.Equals(e.AgentName, query.AgentName, StringComparison.OrdinalIgnoreCase));

        if (query.From.HasValue)
            entries = entries.Where(e => e.Timestamp >= query.From.Value);

        if (query.To.HasValue)
            entries = entries.Where(e => e.Timestamp <= query.To.Value);

        if (query.SuccessOnly.HasValue)
            entries = entries.Where(e => e.Success == query.SuccessOnly.Value);

        return entries;
    }
}
