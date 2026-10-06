using System;
using System.Collections.Generic;

namespace AgenticSystem.Core.Models;

/// <summary>
/// Domain model representing a Golden Set of test cases.
/// </summary>
public class GoldenSet
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public List<GoldenSetCase> Cases { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Domain model representing an individual evaluation case within a Golden Set.
/// </summary>
public class GoldenSetCase
{
    public string Id { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
