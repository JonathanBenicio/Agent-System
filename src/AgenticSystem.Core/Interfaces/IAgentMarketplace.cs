using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Agent marketplace service for discovering, publishing, and installing agent templates.
/// </summary>
public interface IAgentMarketplace
{
    /// <summary>
    /// Publishes an agent template to the marketplace.
    /// </summary>
    Task<AgentMarketplaceEntry> PublishAsync(
        AgentMarketplaceEntry entry,
        CancellationToken ct = default);

    /// <summary>
    /// Searches the marketplace by query, tags, or domain.
    /// </summary>
    Task<IReadOnlyList<AgentMarketplaceEntry>> SearchAsync(
        string? query = null,
        string? domain = null,
        List<string>? tags = null,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a specific marketplace entry by ID.
    /// </summary>
    Task<AgentMarketplaceEntry?> GetEntryAsync(
        string entryId,
        CancellationToken ct = default);

    /// <summary>
    /// Installs an agent from the marketplace into the current system.
    /// </summary>
    Task<AgentInstallResult> InstallAsync(
        AgentInstallRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Clones an existing agent as a new template.
    /// </summary>
    Task<AgentMarketplaceEntry> CloneFromAgentAsync(
        string agentName,
        string? templateName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Rates a marketplace entry.
    /// </summary>
    Task RateAsync(
        string entryId,
        double rating,
        CancellationToken ct = default);

    /// <summary>
    /// Returns featured/popular entries.
    /// </summary>
    Task<IReadOnlyList<AgentMarketplaceEntry>> GetFeaturedAsync(
        int limit = 10,
        CancellationToken ct = default);
}
