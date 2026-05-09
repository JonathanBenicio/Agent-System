using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// In-memory knowledge graph service for Graph RAG.
/// Supports entity/relationship management, BFS traversal, and context generation.
/// </summary>
public class InMemoryKnowledgeGraphService : IKnowledgeGraphService, IKnowledgeGraphStore
{
    private readonly ConcurrentDictionary<string, KnowledgeGraphNode> _nodes = new();
    private readonly ConcurrentDictionary<string, KnowledgeGraphEdge> _edges = new();
    private readonly ILogger<InMemoryKnowledgeGraphService> _logger;

    public InMemoryKnowledgeGraphService(ILogger<InMemoryKnowledgeGraphService> logger)
    {
        _logger = logger;
    }

    // ─── IKnowledgeGraphService ───

    public Task<KnowledgeGraphNode> AddNodeAsync(KnowledgeGraphNode node, CancellationToken ct = default)
    {
        _nodes[node.Id] = node;
        return Task.FromResult(node);
    }

    public Task<KnowledgeGraphEdge> AddEdgeAsync(KnowledgeGraphEdge edge, CancellationToken ct = default)
    {
        _edges[edge.Id] = edge;
        return Task.FromResult(edge);
    }

    public Task<GraphTraversalResult> TraverseAsync(
        string startNodeId, int maxDepth = 3, string? relationFilter = null, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var visitedNodes = new HashSet<string>();
        var resultNodes = new List<KnowledgeGraphNode>();
        var resultEdges = new List<KnowledgeGraphEdge>();
        var queue = new Queue<(string NodeId, int Depth)>();

        queue.Enqueue((startNodeId, 0));
        var maxReached = 0;

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();

            if (!visitedNodes.Add(currentId) || depth > maxDepth) continue;
            maxReached = Math.Max(maxReached, depth);

            if (_nodes.TryGetValue(currentId, out var node))
            {
                resultNodes.Add(node);
            }

            var outEdges = _edges.Values
                .Where(e => e.SourceNodeId == currentId)
                .Where(e => relationFilter == null || e.RelationType.Equals(relationFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var edge in outEdges)
            {
                resultEdges.Add(edge);
                if (!visitedNodes.Contains(edge.TargetNodeId))
                {
                    queue.Enqueue((edge.TargetNodeId, depth + 1));
                }
            }
        }

        sw.Stop();

        return Task.FromResult(new GraphTraversalResult
        {
            Nodes = resultNodes,
            Edges = resultEdges,
            TotalNodesTraversed = visitedNodes.Count,
            MaxDepthReached = maxReached,
            TraversalTime = sw.Elapsed
        });
    }

    public Task<GraphPath?> FindPathAsync(
        string sourceNodeId, string targetNodeId, int maxDepth = 5, CancellationToken ct = default)
    {
        // BFS shortest path
        var visited = new HashSet<string>();
        var queue = new Queue<List<string>>();
        queue.Enqueue([sourceNodeId]);

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var current = path[^1];

            if (current == targetNodeId)
            {
                var edgeIds = new List<string>();
                for (var i = 0; i < path.Count - 1; i++)
                {
                    var edge = _edges.Values.FirstOrDefault(e =>
                        e.SourceNodeId == path[i] && e.TargetNodeId == path[i + 1]);
                    if (edge != null) edgeIds.Add(edge.Id);
                }

                return Task.FromResult<GraphPath?>(new GraphPath
                {
                    NodeIds = path,
                    EdgeIds = edgeIds,
                    TotalWeight = edgeIds.Count
                });
            }

            if (path.Count > maxDepth + 1) continue;

            if (!visited.Add(current)) continue;

            var neighbors = _edges.Values.Where(e => e.SourceNodeId == current).Select(e => e.TargetNodeId);
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    var newPath = new List<string>(path) { neighbor };
                    queue.Enqueue(newPath);
                }
            }
        }

        return Task.FromResult<GraphPath?>(null);
    }

    public Task<IReadOnlyList<KnowledgeGraphNode>> SearchNodesAsync(
        string query, string? entityType = null, int limit = 20, CancellationToken ct = default)
    {
        var results = _nodes.Values
            .Where(n => n.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (n.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(n => entityType == null || n.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<KnowledgeGraphNode>>(results);
    }

    public async Task<string> GenerateGraphContextAsync(string query, int maxHops = 2, CancellationToken ct = default)
    {
        // Find relevant nodes by query
        var matchedNodes = await SearchNodesAsync(query, limit: 5, ct: ct);
        if (matchedNodes.Count == 0) return string.Empty;

        var contextParts = new List<string>();

        foreach (var node in matchedNodes)
        {
            var traversal = await TraverseAsync(node.Id, maxHops, ct: ct);
            contextParts.Add($"Entity: {node.Label} ({node.EntityType})");

            if (!string.IsNullOrEmpty(node.Description))
                contextParts.Add($"  Description: {node.Description}");

            foreach (var edge in traversal.Edges.Take(10))
            {
                var target = traversal.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
                if (target != null)
                    contextParts.Add($"  → {edge.RelationType} → {target.Label}");
            }
        }

        return string.Join("\n", contextParts);
    }

    public Task<IReadOnlyList<KnowledgeGraphNode>> GetNeighborsAsync(
        string nodeId, string? relationFilter = null, CancellationToken ct = default)
    {
        var neighborIds = _edges.Values
            .Where(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId)
            .Where(e => relationFilter == null || e.RelationType.Equals(relationFilter, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.SourceNodeId == nodeId ? e.TargetNodeId : e.SourceNodeId)
            .Distinct();

        var neighbors = neighborIds
            .Select(id => _nodes.TryGetValue(id, out var n) ? n : null)
            .Where(n => n != null)
            .Cast<KnowledgeGraphNode>()
            .ToList();

        return Task.FromResult<IReadOnlyList<KnowledgeGraphNode>>(neighbors);
    }

    public Task<(int NodesCreated, int EdgesCreated)> ExtractAndIngestAsync(
        string text, string? sourceDocumentId = null, CancellationToken ct = default)
    {
        // Simple NER-like extraction (placeholder — production would use LLM extraction)
        _logger.LogDebug("Entity extraction from text ({Length} chars) — placeholder implementation", text.Length);

        // In production, this would call an LLM to extract entities and relationships
        // For now, return zero counts as a no-op placeholder
        return Task.FromResult((0, 0));
    }

    // ─── IKnowledgeGraphStore ───

    public Task UpsertNodeAsync(KnowledgeGraphNode node, CancellationToken ct = default)
    {
        _nodes[node.Id] = node;
        return Task.CompletedTask;
    }

    public Task UpsertEdgeAsync(KnowledgeGraphEdge edge, CancellationToken ct = default)
    {
        _edges[edge.Id] = edge;
        return Task.CompletedTask;
    }

    public Task<KnowledgeGraphNode?> GetNodeAsync(string nodeId, CancellationToken ct = default)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return Task.FromResult(node);
    }

    Task<IReadOnlyList<KnowledgeGraphNode>> IKnowledgeGraphStore.SearchNodesAsync(
        string query, string? entityType, int limit, CancellationToken ct) =>
        SearchNodesAsync(query, entityType, limit, ct);

    public Task<IReadOnlyList<KnowledgeGraphEdge>> GetEdgesFromAsync(
        string nodeId, string? relationType, CancellationToken ct = default)
    {
        var edges = _edges.Values
            .Where(e => e.SourceNodeId == nodeId)
            .Where(e => relationType == null || e.RelationType.Equals(relationType, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<KnowledgeGraphEdge>>(edges);
    }

    public Task<IReadOnlyList<KnowledgeGraphEdge>> GetEdgesToAsync(
        string nodeId, string? relationType, CancellationToken ct = default)
    {
        var edges = _edges.Values
            .Where(e => e.TargetNodeId == nodeId)
            .Where(e => relationType == null || e.RelationType.Equals(relationType, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<KnowledgeGraphEdge>>(edges);
    }
}
