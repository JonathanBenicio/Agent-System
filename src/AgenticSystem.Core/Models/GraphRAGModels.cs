namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Graph RAG — Knowledge Graph & Entity Relationships
// ═══════════════════════════════════════════════════════════

/// <summary>
/// A node in the knowledge graph representing an entity.
/// </summary>
public class KnowledgeGraphNode
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty; // "Person", "Concept", "Document", etc.
    public string? Description { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
    public string? SourceDocumentId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// An edge (relationship) between two nodes in the knowledge graph.
/// </summary>
public class KnowledgeGraphEdge
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string SourceNodeId { get; init; } = string.Empty;
    public string TargetNodeId { get; init; } = string.Empty;
    public string RelationType { get; init; } = string.Empty; // "works_at", "authored", "mentions", etc.
    public double Weight { get; init; } = 1.0;
    public Dictionary<string, string> Properties { get; init; } = new();
    public string? SourceDocumentId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result of a graph traversal query, including the path taken.
/// </summary>
public class GraphTraversalResult
{
    public string QueryId { get; init; } = Guid.NewGuid().ToString("N");
    public List<KnowledgeGraphNode> Nodes { get; init; } = [];
    public List<KnowledgeGraphEdge> Edges { get; init; } = [];
    public List<GraphPath> Paths { get; init; } = [];
    public int TotalNodesTraversed { get; init; }
    public int MaxDepthReached { get; init; }
    public string? ContextSummary { get; init; }
    public TimeSpan TraversalTime { get; init; }
}

/// <summary>
/// A single path through the knowledge graph from source to target.
/// </summary>
public class GraphPath
{
    public List<string> NodeIds { get; init; } = [];
    public List<string> EdgeIds { get; init; } = [];
    public double TotalWeight { get; init; }
    public int Hops => NodeIds.Count > 0 ? NodeIds.Count - 1 : 0;
}

/// <summary>
/// Community/cluster detected within the knowledge graph.
/// </summary>
public class GraphCommunity
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; init; } = string.Empty;
    public List<string> NodeIds { get; init; } = [];
    public string? Summary { get; init; }
    public int Level { get; init; } // Hierarchy level in Leiden/Louvain community detection
}
