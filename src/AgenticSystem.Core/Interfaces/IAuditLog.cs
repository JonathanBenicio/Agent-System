using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Centralized, append-only audit log for all significant system actions.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Records an audit entry. Entries are immutable once recorded.
    /// </summary>
    Task RecordAsync(AuditEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Queries audit entries with filtering, ordering by timestamp descending.
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQuery query, CancellationToken ct = default);

    /// <summary>
    /// Gets the total count of entries matching a query (for pagination).
    /// </summary>
    Task<long> CountAsync(AuditQuery query, CancellationToken ct = default);
}
