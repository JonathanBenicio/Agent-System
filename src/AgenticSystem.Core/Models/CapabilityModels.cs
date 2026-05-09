namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Skill / Capability Registry — Composable Skills
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Formal capability declaration for an agent or skill.
/// Used by the router to match requests to capable agents.
/// </summary>
public class CapabilityDeclaration
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty; // "analysis", "generation", "search", etc.
    public string OwnerType { get; init; } = string.Empty; // "agent" | "skill" | "tool"
    public string OwnerId { get; init; } = string.Empty;
    public List<string> InputTypes { get; init; } = [];  // "text", "image", "json", etc.
    public List<string> OutputTypes { get; init; } = [];
    public List<string> RequiredPermissions { get; init; } = [];
    public double QualityScore { get; init; } = 0.5;
    public double? AverageLatencyMs { get; init; }
    public bool IsComposable { get; init; } = true;
    public List<string> Tags { get; init; } = [];
}

/// <summary>
/// A composable skill that chains multiple capabilities.
/// </summary>
public class ComposableSkill
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<SkillStep> Steps { get; init; } = [];
    public string? CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A step in a composable skill.
/// </summary>
public class SkillStep
{
    public int Order { get; init; }
    public string CapabilityId { get; init; } = string.Empty;
    public Dictionary<string, string> InputMapping { get; init; } = new(); // Maps output of previous step
    public string? ConditionExpression { get; init; } // Skip if condition is false
}

/// <summary>
/// Result of capability matching for a request.
/// </summary>
public class CapabilityMatchResult
{
    public string RequestDescription { get; init; } = string.Empty;
    public List<CapabilityMatch> Matches { get; init; } = [];
    public bool HasExactMatch => Matches.Any(m => m.MatchScore >= 0.9);
}

public class CapabilityMatch
{
    public CapabilityDeclaration Capability { get; init; } = new();
    public double MatchScore { get; init; }
    public string MatchReason { get; init; } = string.Empty;
}
