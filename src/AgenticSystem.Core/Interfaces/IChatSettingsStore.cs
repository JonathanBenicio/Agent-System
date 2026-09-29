using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IChatSettingsStore
{
    Task<ChatSettings?> GetAsync(string tenantId, string userId, CancellationToken ct = default);
    Task SaveAsync(ChatSettings settings, CancellationToken ct = default);
}
