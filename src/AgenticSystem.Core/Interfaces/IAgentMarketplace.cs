using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Service for publishing and discovering agents in a global marketplace.
/// </summary>
public interface IAgentMarketplace
{
    Task<AgentMarketplaceEntry> PublishAsync(AgentSpecification spec, string author, CancellationToken ct = default);
    Task<IReadOnlyList<AgentMarketplaceEntry>> SearchAsync(string? query = null, string? domain = null, List<string>? tags = null, CancellationToken ct = default);
    Task<AgentMarketplaceEntry?> GetAsync(string entryId, CancellationToken ct = default);
    Task RateAgentAsync(string entryId, int rating, string? comment = null, CancellationToken ct = default);
}
