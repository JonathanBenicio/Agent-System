namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Agent Marketplace — Templates, Publishing & Discovery
// ═══════════════════════════════════════════════════════════

/// <summary>
/// A publishable agent template in the marketplace.
/// </summary>
public class AgentMarketplaceEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public AgentTier Tier { get; init; }
    public string SystemPrompt { get; init; } = string.Empty;
    public List<string> Tools { get; init; } = [];
    public List<string> Tags { get; init; } = [];
    public string? AuthorId { get; init; }
    public string? AuthorName { get; init; }
    public int InstallCount { get; set; }
    public double Rating { get; set; }
    public int RatingCount { get; set; }
    public MarketplaceEntryStatus Status { get; set; } = MarketplaceEntryStatus.Draft;
    public string? Version { get; init; }
    public string? IconUrl { get; init; }
    public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum MarketplaceEntryStatus
{
    Draft,
    Published,
    UnderReview,
    Deprecated,
    Removed
}

/// <summary>
/// Request to install an agent from the marketplace.
/// </summary>
public class AgentInstallRequest
{
    public string MarketplaceEntryId { get; init; } = string.Empty;
    public string? CustomName { get; init; }
    public Dictionary<string, string>? OverrideParameters { get; init; }
    public string InstalledBy { get; init; } = string.Empty;
}

/// <summary>
/// Result of installing an agent from the marketplace.
/// </summary>
public class AgentInstallResult
{
    public bool Success { get; init; }
    public string? InstalledAgentName { get; init; }
    public string? Message { get; init; }
}
