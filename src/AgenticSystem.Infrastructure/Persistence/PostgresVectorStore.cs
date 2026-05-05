using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Implementação PostgreSQL de IVectorStore com busca textual.
/// Utiliza tabela vector_documents com coluna metadata JSONB.
/// Para busca vetorial real com pgvector, substituir CalculateRelevance por cosine similarity SQL.
/// </summary>
public class PostgresVectorStore : IVectorStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresVectorStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresVectorStore(string connectionString, ILogger<PostgresVectorStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task UpsertAsync(EmbeddingDocument document)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            const string sql = """
                INSERT INTO vector_documents (id, content, type, collection, embedding, metadata, indexed_at)
                VALUES (@id, @content, @type, @collection, @embedding, @metadata::jsonb, @indexedAt)
                ON CONFLICT (id) DO UPDATE SET
                    content = EXCLUDED.content,
                    type = EXCLUDED.type,
                    collection = EXCLUDED.collection,
                    embedding = EXCLUDED.embedding,
                    metadata = EXCLUDED.metadata,
                    indexed_at = EXCLUDED.indexed_at
                """;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", document.Id);
            cmd.Parameters.AddWithValue("content", document.Content);
            cmd.Parameters.AddWithValue("type", document.Type);
            cmd.Parameters.AddWithValue("collection", document.Collection);
            cmd.Parameters.AddWithValue("embedding", (object?)document.Embedding ?? DBNull.Value);
            cmd.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(document.Metadata, JsonOptions));
            cmd.Parameters.AddWithValue("indexedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug("Upserted document {Id} in collection {Collection} to PostgreSQL", document.Id, document.Collection);
        });
    }

    public async Task DeleteAsync(string id, string? collection = null)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var sql = "DELETE FROM vector_documents WHERE id = @id";
            if (!string.IsNullOrWhiteSpace(collection))
            {
                sql += " AND collection = @collection";
            }

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            if (!string.IsNullOrWhiteSpace(collection))
            {
                cmd.Parameters.AddWithValue("collection", collection);
            }

            var deleted = await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug("Deleted {Count} document(s) with id {Id} from collection {Collection}",
                deleted, id, collection ?? "*");
        });
    }

    public async Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var sql = """
            SELECT id, content, type, collection, embedding, metadata, indexed_at,
                   ts_rank(to_tsvector('simple', content), plainto_tsquery('simple', @query)) AS score
            FROM vector_documents
            WHERE to_tsvector('simple', content) @@ plainto_tsquery('simple', @query)
            """;

        if (scope != SearchScope.All)
            sql += " AND collection = @collection";

        sql += " ORDER BY score DESC LIMIT @limit";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("query", query);
        cmd.Parameters.AddWithValue("limit", maxResults);

        if (scope != SearchScope.All)
            cmd.Parameters.AddWithValue("collection", ScopeToCollection(scope));

        var matches = await ReadMatchesAsync(cmd);
        sw.Stop();

        return new SearchResult
        {
            Query = query,
            Scope = scope,
            Matches = matches.Take(maxResults).ToList(),
            TotalFound = matches.Count,
            ExecutionTime = sw.Elapsed
        };
    }

    public async Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var normalizedQuery = query.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(normalizedQuery) && normalizedQuery != "*";

        var sql = hasQuery
            ? """
                SELECT id, content, type, collection, embedding, metadata, indexed_at,
                       ts_rank(to_tsvector('simple', content), plainto_tsquery('simple', @query)) AS score
                FROM vector_documents
                WHERE to_tsvector('simple', content) @@ plainto_tsquery('simple', @query)
                """
            : """
                SELECT id, content, type, collection, embedding, metadata, indexed_at,
                       1.0::double precision AS score
                FROM vector_documents
                WHERE TRUE
                """;

        var paramIndex = 0;
        var extraParams = new List<NpgsqlParameter>();

        if (filters.TryGetValue("type", out var typeFilter))
        {
            sql += $" AND type = @p{paramIndex}";
            extraParams.Add(new NpgsqlParameter($"p{paramIndex++}", typeFilter));
        }
        if (filters.TryGetValue("collection", out var collFilter))
        {
            sql += $" AND collection = @p{paramIndex}";
            extraParams.Add(new NpgsqlParameter($"p{paramIndex++}", collFilter));
        }
        if (filters.TryGetValue("id", out var idFilter))
        {
            sql += $" AND id = @p{paramIndex}";
            extraParams.Add(new NpgsqlParameter($"p{paramIndex++}", idFilter));
        }

        foreach (var (key, value) in filters)
        {
            if (key is "type" or "collection" or "id")
            {
                continue;
            }

            var keyParameter = $"p{paramIndex++}";
            var valueParameter = $"p{paramIndex++}";
            sql += $" AND metadata ->> @{keyParameter} = @{valueParameter}";
            extraParams.Add(new NpgsqlParameter(keyParameter, key));
            extraParams.Add(new NpgsqlParameter(valueParameter, value));
        }

        sql += hasQuery ? " ORDER BY score DESC LIMIT 10" : " ORDER BY indexed_at DESC";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (hasQuery)
        {
            cmd.Parameters.AddWithValue("query", normalizedQuery);
        }
        foreach (var p in extraParams)
            cmd.Parameters.Add(p);

        var matches = await ReadMatchesAsync(cmd);
        sw.Stop();

        return new SearchResult
        {
            Query = query,
            Matches = matches,
            TotalFound = matches.Count,
            ExecutionTime = sw.Elapsed
        };
    }

    public async Task<IEnumerable<string>> GetCollectionsAsync()
    {
        const string sql = "SELECT DISTINCT collection FROM vector_documents ORDER BY collection";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var collections = new List<string>();
        while (await reader.ReadAsync())
            collections.Add(reader.GetString(0));

        return collections;
    }

    public async Task CleanupOldDocumentsAsync(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        const string sql = "DELETE FROM vector_documents WHERE indexed_at < @cutoff";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cutoff", cutoff);

        var deleted = await cmd.ExecuteNonQueryAsync();
        _logger.LogInformation("Cleaned up {Count} old documents from PostgreSQL (older than {Days} days)",
            deleted, olderThan.TotalDays);
    }

    private static async Task<List<SearchMatch>> ReadMatchesAsync(NpgsqlCommand cmd)
    {
        var matches = new List<SearchMatch>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var content = reader.GetString(1);
            var embedding = reader.IsDBNull(4) ? Array.Empty<float>() : reader.GetFieldValue<float[]>(4);
            var metadataJson = reader.IsDBNull(5) ? "{}" : reader.GetString(5);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, JsonOptions)
                           ?? new Dictionary<string, string>();

            matches.Add(new SearchMatch
            {
                Id = reader.GetString(0),
                Content = content,
                Type = reader.GetString(2),
                Collection = reader.GetString(3),
                Score = reader.GetDouble(7),
                Metadata = metadata,
                Snippet = content.Length > 300 ? content[..300] + "..." : content,
                Embedding = embedding,
                IndexedAt = reader.GetDateTime(6)
            });
        }

        return matches;
    }

    private static string ScopeToCollection(SearchScope scope) => scope switch
    {
        SearchScope.Notes => "notes",
        SearchScope.Agents => "agents",
        SearchScope.Decisions => "decisions",
        SearchScope.Domain => "domain",
        _ => ""
    };

    private static readonly Random s_jitter = new();

    private async Task ExecuteWithRetryAsync(Func<Task> action, int maxRetries = 3)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (NpgsqlException ex) when (ex.IsTransient && attempt < maxRetries - 1)
            {
                var baseDelay = 100 * Math.Pow(2, attempt);
                var jitter = s_jitter.Next(0, (int)(baseDelay * 0.5));
                var delay = TimeSpan.FromMilliseconds(baseDelay + jitter);
                _logger.LogWarning(ex, "Transient PostgreSQL error (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms.",
                    attempt + 1, maxRetries, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
        }
    }
}
