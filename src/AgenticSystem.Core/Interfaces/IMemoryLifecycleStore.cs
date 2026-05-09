using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Persistence store for enhanced memory entries and their lifecycle states.
/// </summary>
public interface IMemoryLifecycleStore
{
    Task SaveAsync(EnhancedMemoryEntry entry, CancellationToken ct = default);
    Task<EnhancedMemoryEntry?> GetAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<EnhancedMemoryEntry>> ListAsync(string? sessionId = null, string? agentName = null, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
