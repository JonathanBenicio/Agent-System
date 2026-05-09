using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Knowledge graph service for Graph RAG.
/// Manages entity/relationship storage, traversal, and context generation.
/// </summary>
public interface IKnowledgeGraphService
{
    /// <summary>
    /// Adds a node (entity) to the knowledge graph.
    /// </summary>
    Task<KnowledgeGraphNode> AddNodeAsync(KnowledgeGraphNode node, CancellationToken ct = default);

    /// <summary>
    /// Adds an edge (relationship) between two nodes.
    /// </summary>
    Task<KnowledgeGraphEdge> AddEdgeAsync(KnowledgeGraphEdge edge, CancellationToken ct = default);

    /// <summary>
    /// Multi-hop traversal: finds paths between entities up to a maximum depth.
    /// </summary>
    Task<GraphTraversalResult> TraverseAsync(
        string startNodeId,
        int maxDepth = 3,
        string? relationFilter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Finds the shortest path between two entities.
    /// </summary>
    Task<GraphPath?> FindPathAsync(
        string sourceNodeId,
        string targetNodeId,
        int maxDepth = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Searches nodes by label or entity type.
    /// </summary>
    Task<IReadOnlyList<KnowledgeGraphNode>> SearchNodesAsync(
        string query,
        string? entityType = null,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a context summary from graph traversal for RAG injection.
    /// </summary>
    Task<string> GenerateGraphContextAsync(
        string query,
        int maxHops = 2,
        CancellationToken ct = default);

    /// <summary>
    /// Returns related nodes for a given entity (1-hop neighborhood).
    /// </summary>
    Task<IReadOnlyList<KnowledgeGraphNode>> GetNeighborsAsync(
        string nodeId,
        string? relationFilter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts entities and relationships from text and upserts into the graph.
    /// </summary>
    Task<(int NodesCreated, int EdgesCreated)> ExtractAndIngestAsync(
        string text,
        string? sourceDocumentId = null,
        CancellationToken ct = default);
}

/// <summary>
/// Persistence store for knowledge graph data.
/// </summary>
public interface IKnowledgeGraphStore
{
    Task UpsertNodeAsync(KnowledgeGraphNode node, CancellationToken ct = default);
    Task UpsertEdgeAsync(KnowledgeGraphEdge edge, CancellationToken ct = default);
    Task<KnowledgeGraphNode?> GetNodeAsync(string nodeId, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeGraphNode>> SearchNodesAsync(string query, string? entityType, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeGraphEdge>> GetEdgesFromAsync(string nodeId, string? relationType, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeGraphEdge>> GetEdgesToAsync(string nodeId, string? relationType, CancellationToken ct = default);
}
