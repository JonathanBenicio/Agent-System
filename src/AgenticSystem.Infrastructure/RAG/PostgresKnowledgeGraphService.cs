using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.RAG;

/// <summary>
/// PostgreSQL implementation of the Knowledge Graph Service and Store using EF Core.
/// </summary>
public class PostgresKnowledgeGraphService : IKnowledgeGraphService, IKnowledgeGraphStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresKnowledgeGraphService> _logger;
    private readonly IChatClient _chatClient;

    public PostgresKnowledgeGraphService(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresKnowledgeGraphService> logger,
        IChatClient chatClient)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _chatClient = chatClient;
    }

    // ─── IKnowledgeGraphService ───

    public async Task<KnowledgeGraphNode> AddNodeAsync(KnowledgeGraphNode node, CancellationToken ct = default)
    {
        await UpsertNodeAsync(node, ct);
        return node;
    }

    public async Task<KnowledgeGraphEdge> AddEdgeAsync(KnowledgeGraphEdge edge, CancellationToken ct = default)
    {
        await UpsertEdgeAsync(edge, ct);
        return edge;
    }

    public async Task<GraphTraversalResult> TraverseAsync(
        string startNodeId, int maxDepth = 3, string? relationFilter = null, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var visitedNodeIds = new HashSet<string>();
        var resultNodes = new List<KnowledgeGraphNode>();
        var resultEdges = new List<KnowledgeGraphEdge>();
        var queue = new Queue<(string NodeId, int Depth)>();

        queue.Enqueue((startNodeId, 0));
        var maxReached = 0;

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();

            if (!visitedNodeIds.Add(currentId) || depth > maxDepth) continue;
            maxReached = Math.Max(maxReached, depth);

            var nodeEntity = await dbContext.KnowledgeGraphNodes.FindAsync([currentId], ct);
            if (nodeEntity != null)
            {
                resultNodes.Add(MapToModel(nodeEntity));
            }

            var query = dbContext.KnowledgeGraphEdges.Where(e => e.SourceNodeId == currentId);
            if (!string.IsNullOrEmpty(relationFilter))
            {
                query = query.Where(e => e.RelationType == relationFilter);
            }

            var outEdges = await query.ToListAsync(ct);

            foreach (var edgeEntity in outEdges)
            {
                resultEdges.Add(MapToModel(edgeEntity));
                if (!visitedNodeIds.Contains(edgeEntity.TargetNodeId))
                {
                    queue.Enqueue((edgeEntity.TargetNodeId, depth + 1));
                }
            }
        }

        sw.Stop();

        return new GraphTraversalResult
        {
            Nodes = resultNodes,
            Edges = resultEdges,
            TotalNodesTraversed = visitedNodeIds.Count,
            MaxDepthReached = maxReached,
            TraversalTime = sw.Elapsed
        };
    }

    public async Task<GraphPath?> FindPathAsync(
        string sourceNodeId, string targetNodeId, int maxDepth = 5, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
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
                    var edge = await dbContext.KnowledgeGraphEdges.FirstOrDefaultAsync(e =>
                        e.SourceNodeId == path[i] && e.TargetNodeId == path[i + 1], ct);
                    if (edge != null) edgeIds.Add(edge.Id);
                }

                return new GraphPath
                {
                    NodeIds = path,
                    EdgeIds = edgeIds,
                    TotalWeight = edgeIds.Count
                };
            }

            if (path.Count > maxDepth + 1) continue;

            if (!visited.Add(current)) continue;

            var neighbors = await dbContext.KnowledgeGraphEdges
                .Where(e => e.SourceNodeId == current)
                .Select(e => e.TargetNodeId)
                .ToListAsync(ct);

            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    var newPath = new List<string>(path) { neighbor };
                    queue.Enqueue(newPath);
                }
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<KnowledgeGraphNode>> SearchNodesAsync(
        string query, string? entityType = null, int limit = 20, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var dbQuery = dbContext.KnowledgeGraphNodes.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
        {
            dbQuery = dbQuery.Where(n => n.EntityType == entityType);
        }

        dbQuery = dbQuery.Where(n => EF.Functions.ILike(n.Label, $"%{query}%") || 
                                     (n.Description != null && EF.Functions.ILike(n.Description, $"%{query}%")));

        var results = await dbQuery.Take(limit).ToListAsync(ct);
        return results.Select(MapToModel).ToList();
    }

    public async Task<string> GenerateGraphContextAsync(string query, int maxHops = 2, CancellationToken ct = default)
    {
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

    public async Task<IReadOnlyList<KnowledgeGraphNode>> GetNeighborsAsync(
        string nodeId, string? relationFilter = null, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var edgesQuery = dbContext.KnowledgeGraphEdges.Where(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId);
        
        if (!string.IsNullOrEmpty(relationFilter))
        {
            edgesQuery = edgesQuery.Where(e => e.RelationType == relationFilter);
        }

        var edges = await edgesQuery.ToListAsync(ct);
        var neighborIds = edges.Select(e => e.SourceNodeId == nodeId ? e.TargetNodeId : e.SourceNodeId).Distinct().ToList();

        var neighbors = await dbContext.KnowledgeGraphNodes
            .Where(n => neighborIds.Contains(n.Id))
            .ToListAsync(ct);

        return neighbors.Select(MapToModel).ToList();
    }

    public async Task<(int NodesCreated, int EdgesCreated)> ExtractAndIngestAsync(
        string text, string? sourceDocumentId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (0, 0);
        }

        _logger.LogInformation("Starting LLM-based entity and relationship extraction from text ({Length} characters)", text.Length);

        var prompt = $$"""
            You are an advanced knowledge graph extraction assistant. Your task is to analyze the input text and extract all important entities (nodes) and their relationships (edges).

            Formulate your response as a strict JSON object with two main arrays: "nodes" and "edges". Do not include any markdown formatting wrappers (like ```json ... ```) or other text outside the JSON.

            Output format:
            {
              "nodes": [
                {
                  "label": "The name or primary label of the entity",
                  "type": "The category of the entity, e.g., Person, Organization, Location, Concept, Technology, Product, Event",
                  "description": "A short description explaining this entity in the context of the text",
                  "properties": {
                    "additional_key": "additional_value"
                  }
                }
              ],
              "edges": [
                {
                  "sourceLabel": "The label of the source entity",
                  "sourceType": "The type of the source entity",
                  "targetLabel": "The label of the target entity",
                  "targetType": "The type of the target entity",
                  "relation": "A concise verb/predicate representing the relationship (e.g., works_at, parent_of, created, located_in, uses, belongs_to)",
                  "weight": 1.0,
                  "properties": {
                    "context": "Context or details of how this relationship is described in the text"
                  }
                }
              ]
            }

            Guidelines:
            1. Keep entity labels concise but unique (e.g., use "Google LLC" or "John Doe" rather than pronouns or general terms).
            2. Normalize relation types to lowercase_snake_case (e.g., "acquired", "founded_by", "member_of").
            3. Make sure every sourceLabel and targetLabel used in "edges" is also present as a node in the "nodes" array.
            4. Only extract entities and relationships that are explicitly mentioned or clearly implied in the text.

            Input Text:
            {{text}}
            """;

        try
        {
            var options = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.Json
            };

            var response = await _chatClient.GetResponseAsync(prompt, options, ct);
            var json = response.Text;

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("LLM returned an empty response for entity extraction.");
                return (0, 0);
            }

            // Clean up backticks if any
            json = json.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                json = json.Substring(7);
            }
            if (json.EndsWith("```"))
            {
                json = json.Substring(0, json.Length - 3);
            }
            json = json.Trim();

            var extractionResult = JsonSerializer.Deserialize<GraphExtractionDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (extractionResult == null)
            {
                _logger.LogWarning("Failed to deserialize LLM response as GraphExtractionDto.");
                return (0, 0);
            }

            var nodesCreated = 0;
            var edgesCreated = 0;

            // Normalize and upsert nodes
            var nodeMap = new Dictionary<string, string>(); // Maps normalized Label+Type to exact generated Node ID

            foreach (var nodeDto in extractionResult.Nodes ?? new List<NodeExtractionDto>())
            {
                if (string.IsNullOrWhiteSpace(nodeDto.Label) || string.IsNullOrWhiteSpace(nodeDto.Type))
                {
                    continue;
                }

                var nodeId = NormalizeId(nodeDto.Label, nodeDto.Type);
                nodeMap[$"{nodeDto.Type.Trim().ToLowerInvariant()}:{nodeDto.Label.Trim().ToLowerInvariant()}"] = nodeId;

                var node = new KnowledgeGraphNode
                {
                    Id = nodeId,
                    Label = nodeDto.Label.Trim(),
                    EntityType = nodeDto.Type.Trim(),
                    Description = nodeDto.Description,
                    Properties = nodeDto.Properties ?? new(),
                    SourceDocumentId = sourceDocumentId,
                    CreatedAt = DateTime.UtcNow
                };

                await UpsertNodeAsync(node, ct);
                nodesCreated++;
            }

            // Normalize and upsert edges
            foreach (var edgeDto in extractionResult.Edges ?? new List<EdgeExtractionDto>())
            {
                if (string.IsNullOrWhiteSpace(edgeDto.SourceLabel) || string.IsNullOrWhiteSpace(edgeDto.SourceType) ||
                    string.IsNullOrWhiteSpace(edgeDto.TargetLabel) || string.IsNullOrWhiteSpace(edgeDto.TargetType) ||
                    string.IsNullOrWhiteSpace(edgeDto.Relation))
                {
                    continue;
                }

                var sourceKey = $"{edgeDto.SourceType.Trim().ToLowerInvariant()}:{edgeDto.SourceLabel.Trim().ToLowerInvariant()}";
                var targetKey = $"{edgeDto.TargetType.Trim().ToLowerInvariant()}:{edgeDto.TargetLabel.Trim().ToLowerInvariant()}";

                // Ensure the source and target nodes exist (or create placeholders if the LLM forgot to list them in "nodes")
                if (!nodeMap.TryGetValue(sourceKey, out var sourceNodeId))
                {
                    sourceNodeId = NormalizeId(edgeDto.SourceLabel, edgeDto.SourceType);
                    nodeMap[sourceKey] = sourceNodeId;

                    var placeholderSource = new KnowledgeGraphNode
                    {
                        Id = sourceNodeId,
                        Label = edgeDto.SourceLabel.Trim(),
                        EntityType = edgeDto.SourceType.Trim(),
                        SourceDocumentId = sourceDocumentId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await UpsertNodeAsync(placeholderSource, ct);
                    nodesCreated++;
                }

                if (!nodeMap.TryGetValue(targetKey, out var targetNodeId))
                {
                    targetNodeId = NormalizeId(edgeDto.TargetLabel, edgeDto.TargetType);
                    nodeMap[targetKey] = targetNodeId;

                    var placeholderTarget = new KnowledgeGraphNode
                    {
                        Id = targetNodeId,
                        Label = edgeDto.TargetLabel.Trim(),
                        EntityType = edgeDto.TargetType.Trim(),
                        SourceDocumentId = sourceDocumentId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await UpsertNodeAsync(placeholderTarget, ct);
                    nodesCreated++;
                }

                // Create edge ID by hashing source, target, and relationship type
                var relationNormalized = edgeDto.Relation.Trim().ToLowerInvariant();
                var edgeId = NormalizeId($"{sourceNodeId}_{targetNodeId}_{relationNormalized}", "edge");

                var edge = new KnowledgeGraphEdge
                {
                    Id = edgeId,
                    SourceNodeId = sourceNodeId,
                    TargetNodeId = targetNodeId,
                    RelationType = edgeDto.Relation.Trim(),
                    Weight = edgeDto.Weight ?? 1.0,
                    Properties = edgeDto.Properties ?? new(),
                    SourceDocumentId = sourceDocumentId,
                    CreatedAt = DateTime.UtcNow
                };

                await UpsertEdgeAsync(edge, ct);
                edgesCreated++;
            }

            _logger.LogInformation("Successfully ingested {NodesCount} nodes and {EdgesCount} edges into PostgreSQL Knowledge Graph", nodesCreated, edgesCreated);
            return (nodesCreated, edgesCreated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during entity and relationship extraction from text.");
            return (0, 0);
        }
    }

    private static string NormalizeId(string label, string entityType)
    {
        var combined = $"{entityType.Trim().ToLowerInvariant()}:{label.Trim().ToLowerInvariant()}";
        var normalized = new string(combined.Where(c => char.IsLetterOrDigit(c) || c == ':' || c == '-' || c == '_').ToArray());
        return normalized;
    }

    private class GraphExtractionDto
    {
        public List<NodeExtractionDto>? Nodes { get; set; }
        public List<EdgeExtractionDto>? Edges { get; set; }
    }

    private class NodeExtractionDto
    {
        public string? Label { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
    }

    private class EdgeExtractionDto
    {
        public string? SourceLabel { get; set; }
        public string? SourceType { get; set; }
        public string? TargetLabel { get; set; }
        public string? TargetType { get; set; }
        public string? Relation { get; set; }
        public double? Weight { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
    }

    // ─── IKnowledgeGraphStore ───

    public async Task UpsertNodeAsync(KnowledgeGraphNode node, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await dbContext.KnowledgeGraphNodes.FirstOrDefaultAsync(n => n.Id == node.Id, ct);
        if (entity == null)
        {
            entity = new KnowledgeGraphNodeEntity
            {
                Id = node.Id,
                CreatedAt = node.CreatedAt
            };
            dbContext.KnowledgeGraphNodes.Add(entity);
        }

        entity.Label = node.Label;
        entity.EntityType = node.EntityType;
        entity.Description = node.Description;
        entity.SourceDocumentId = node.SourceDocumentId;
        entity.PropertiesJson = JsonSerializer.Serialize(node.Properties);

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpsertEdgeAsync(KnowledgeGraphEdge edge, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await dbContext.KnowledgeGraphEdges.FirstOrDefaultAsync(e => e.Id == edge.Id, ct);
        if (entity == null)
        {
            entity = new KnowledgeGraphEdgeEntity
            {
                Id = edge.Id,
                CreatedAt = edge.CreatedAt
            };
            dbContext.KnowledgeGraphEdges.Add(entity);
        }

        entity.SourceNodeId = edge.SourceNodeId;
        entity.TargetNodeId = edge.TargetNodeId;
        entity.RelationType = edge.RelationType;
        entity.Weight = edge.Weight;
        entity.SourceDocumentId = edge.SourceDocumentId;
        entity.PropertiesJson = JsonSerializer.Serialize(edge.Properties);

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<KnowledgeGraphNode?> GetNodeAsync(string nodeId, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await dbContext.KnowledgeGraphNodes.FindAsync([nodeId], ct);
        return entity == null ? null : MapToModel(entity);
    }

    async Task<IReadOnlyList<KnowledgeGraphNode>> IKnowledgeGraphStore.SearchNodesAsync(
        string query, string? entityType, int limit, CancellationToken ct) =>
        await SearchNodesAsync(query, entityType, limit, ct);

    public async Task<IReadOnlyList<KnowledgeGraphEdge>> GetEdgesFromAsync(
        string nodeId, string? relationType, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = dbContext.KnowledgeGraphEdges.Where(e => e.SourceNodeId == nodeId);
        if (!string.IsNullOrEmpty(relationType))
        {
            query = query.Where(e => e.RelationType == relationType);
        }

        var entities = await query.ToListAsync(ct);
        return entities.Select(MapToModel).ToList();
    }

    public async Task<IReadOnlyList<KnowledgeGraphEdge>> GetEdgesToAsync(
        string nodeId, string? relationType, CancellationToken ct = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = dbContext.KnowledgeGraphEdges.Where(e => e.TargetNodeId == nodeId);
        if (!string.IsNullOrEmpty(relationType))
        {
            query = query.Where(e => e.RelationType == relationType);
        }

        var entities = await query.ToListAsync(ct);
        return entities.Select(MapToModel).ToList();
    }

    // ─── Mapping Helpers ───

    private static KnowledgeGraphNode MapToModel(KnowledgeGraphNodeEntity entity)
    {
        return new KnowledgeGraphNode
        {
            Id = entity.Id,
            Label = entity.Label,
            EntityType = entity.EntityType,
            Description = entity.Description,
            Properties = string.IsNullOrEmpty(entity.PropertiesJson) 
                ? new() 
                : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.PropertiesJson) ?? new(),
            SourceDocumentId = entity.SourceDocumentId,
            CreatedAt = entity.CreatedAt
        };
    }

    private static KnowledgeGraphEdge MapToModel(KnowledgeGraphEdgeEntity entity)
    {
        return new KnowledgeGraphEdge
        {
            Id = entity.Id,
            SourceNodeId = entity.SourceNodeId,
            TargetNodeId = entity.TargetNodeId,
            RelationType = entity.RelationType,
            Weight = entity.Weight,
            Properties = string.IsNullOrEmpty(entity.PropertiesJson) 
                ? new() 
                : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.PropertiesJson) ?? new(),
            SourceDocumentId = entity.SourceDocumentId,
            CreatedAt = entity.CreatedAt
        };
    }
}
