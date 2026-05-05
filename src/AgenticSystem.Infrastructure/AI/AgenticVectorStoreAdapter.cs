using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.AI;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace AgenticSystem.Infrastructure.AI;

using TextEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>;

/// <summary>
/// Adapter que expõe o IVectorStore do AgenticSystem como Microsoft.Extensions.VectorData.VectorStore.
/// Permite integração com pipelines M.E.AI que esperam a abstração VectorStore padrão.
/// </summary>
public class AgenticVectorStoreAdapter : VectorStore
{
    private readonly IVectorStore _agenticStore;
    private readonly TextEmbeddingGenerator? _embeddingGenerator;
    private readonly ConcurrentDictionary<string, byte> _managedCollections = new(StringComparer.OrdinalIgnoreCase);
    private static readonly VectorStoreMetadata s_storeMetadata = new()
    {
        VectorStoreSystemName = "agentic",
        VectorStoreName = "AgenticSystem"
    };

    public AgenticVectorStoreAdapter(IVectorStore agenticStore, TextEmbeddingGenerator? embeddingGenerator = null)
    {
        _agenticStore = agenticStore;
        _embeddingGenerator = embeddingGenerator;
    }

    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name, VectorStoreCollectionDefinition? definition = null)
    {
        if (typeof(TKey) != typeof(string) || typeof(TRecord) != typeof(EmbeddingDocument))
        {
            throw new NotSupportedException(
                "AgenticVectorStoreAdapter currently supports GetCollection<string, EmbeddingDocument>() only. " +
                "Use GetDynamicCollection() for dictionary-based access or acesse IVectorStore do DI para o contrato custom.");
        }

        return (VectorStoreCollection<TKey, TRecord>)(object)new AgenticEmbeddingCollection(
            name,
            definition,
            _agenticStore,
            _embeddingGenerator,
            _managedCollections);
    }

    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name, VectorStoreCollectionDefinition definition)
    {
        return new AgenticDynamicCollection(name, definition, _agenticStore, _embeddingGenerator, _managedCollections);
    }

    public override IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
    {
        return ListCollectionsInternal(cancellationToken);
    }

    private async IAsyncEnumerable<string> ListCollectionsInternal(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var collections = await _agenticStore.GetCollectionsAsync();
        foreach (var name in collections.Concat(_managedCollections.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            yield return name;
        }
    }

    public override async Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_managedCollections.ContainsKey(name))
        {
            return true;
        }

        var collections = await _agenticStore.GetCollectionsAsync();
        return collections.Any(c => c == name);
    }

    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        // No-op: InMemoryVectorStore doesn't support collection deletion via this interface
        return Task.CompletedTask;
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IVectorStore))
            return _agenticStore;
        if (serviceType == typeof(VectorStoreMetadata))
            return s_storeMetadata;
        return null;
    }

    /// <summary>
    /// Busca semântica usando o IVectorStore existente.
    /// </summary>
    public async Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10)
    {
        return await _agenticStore.SearchAsync(query, scope, maxResults);
    }

    /// <summary>
    /// Upsert de documento vetorial.
    /// </summary>
    public async Task UpsertAsync(string id, string content, string collection, string type, Dictionary<string, string>? metadata = null)
    {
        if (_embeddingGenerator is null)
        {
            throw new InvalidOperationException("No embedding generator is configured for vector upserts.");
        }

        var embedding = await _embeddingGenerator.GenerateAsync(content);

        var doc = new EmbeddingDocument
        {
            Id = id,
            Content = content,
            Collection = collection,
            Type = type,
            Embedding = embedding.Vector.ToArray(),
            Metadata = metadata ?? new()
        };

        await _agenticStore.UpsertAsync(doc);
    }

    private sealed class AgenticEmbeddingCollection : VectorStoreCollection<string, EmbeddingDocument>
    {
        private readonly string _name;
        private readonly VectorStoreCollectionDefinition? _definition;
        private readonly IVectorStore _store;
        private readonly TextEmbeddingGenerator? _embeddingGenerator;
        private readonly ConcurrentDictionary<string, byte> _managedCollections;
        private readonly VectorStoreCollectionMetadata _metadata;

        public AgenticEmbeddingCollection(
            string name,
            VectorStoreCollectionDefinition? definition,
            IVectorStore store,
            TextEmbeddingGenerator? embeddingGenerator,
            ConcurrentDictionary<string, byte> managedCollections)
        {
            _name = name;
            _definition = definition;
            _store = store;
            _embeddingGenerator = embeddingGenerator;
            _managedCollections = managedCollections;
            _metadata = new VectorStoreCollectionMetadata
            {
                VectorStoreSystemName = s_storeMetadata.VectorStoreSystemName,
                VectorStoreName = s_storeMetadata.VectorStoreName,
                CollectionName = name
            };
        }

        public override string Name => _name;

        public override async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_managedCollections.ContainsKey(_name))
            {
                return true;
            }

            var collections = await _store.GetCollectionsAsync();
            return collections.Any(c => string.Equals(c, _name, StringComparison.OrdinalIgnoreCase));
        }

        public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _managedCollections.TryAdd(_name, 0);
            return Task.CompletedTask;
        }

        public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _managedCollections.TryRemove(_name, out _);

            var documents = await LoadDocumentsAsync(includeVectors: false, cancellationToken);
            foreach (var document in documents)
            {
                await _store.DeleteAsync(document.Id, _name);
            }
        }

        public override async Task<EmbeddingDocument?> GetAsync(string key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _store.SearchWithFiltersAsync(string.Empty, new Dictionary<string, string>
            {
                ["collection"] = _name,
                ["id"] = key
            });

            return result.Matches
                .Select(match => ToEmbeddingDocument(match, includeVectors: options?.IncludeVectors ?? false))
                .FirstOrDefault();
        }

        public override async IAsyncEnumerable<EmbeddingDocument> GetAsync(
            Expression<Func<EmbeddingDocument, bool>> filter,
            int top,
            FilteredRecordRetrievalOptions<EmbeddingDocument>? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var predicate = filter.Compile();
            var includeVectors = options?.IncludeVectors ?? false;
            var documents = await LoadDocumentsAsync(includeVectors: true, cancellationToken);

            foreach (var document in documents.Where(predicate).Take(top))
            {
                yield return includeVectors ? document : WithoutVectors(document);
            }
        }

        public override Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _store.DeleteAsync(key, _name);
        }

        public override Task UpsertAsync(EmbeddingDocument record, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _managedCollections.TryAdd(_name, 0);
            record.Collection = _name;
            return _store.UpsertAsync(record);
        }

        public override async Task UpsertAsync(IEnumerable<EmbeddingDocument> records, CancellationToken cancellationToken = default)
        {
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UpsertAsync(record, cancellationToken);
            }
        }

        public override async IAsyncEnumerable<VectorSearchResult<EmbeddingDocument>> SearchAsync<TInput>(
            TInput searchValue,
            int top,
            VectorSearchOptions<EmbeddingDocument>? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var documents = await LoadDocumentsAsync(includeVectors: true, cancellationToken);
            var filter = options?.Filter?.Compile();
            var includeVectors = options?.IncludeVectors ?? false;
            var queryVector = await TryGetQueryVectorAsync(searchValue, cancellationToken);
            var queryText = searchValue as string;

            if (searchValue is not null && queryText is null && queryVector is null)
            {
                throw new NotSupportedException(
                    "AgenticVectorStoreAdapter supports search inputs of type string, float[], ReadOnlyMemory<float> and Embedding<float>.");
            }

            IEnumerable<(EmbeddingDocument Document, double Score)> ranked = documents
                .Select(document => (Document: document, Score: CalculateScore(queryText, queryVector, document)))
                .Where(item => item.Score > 0);

            if (filter is not null)
            {
                ranked = ranked.Where(item => filter(item.Document));
            }

            if (options?.ScoreThreshold is { } threshold)
            {
                ranked = ranked.Where(item => item.Score >= threshold);
            }

            foreach (var item in ranked
                .OrderByDescending(item => item.Score)
                .Skip(options?.Skip ?? 0)
                .Take(top))
            {
                yield return new VectorSearchResult<EmbeddingDocument>(
                    includeVectors ? item.Document : WithoutVectors(item.Document),
                    item.Score);
            }
        }

        public override object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceType == typeof(IVectorStore))
                return _store;
            if (serviceType == typeof(VectorStoreCollectionMetadata))
                return _metadata;
            if (serviceType == typeof(VectorStoreCollectionDefinition))
                return _definition;
            return null;
        }

        private async Task<List<EmbeddingDocument>> LoadDocumentsAsync(bool includeVectors, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _store.SearchWithFiltersAsync(string.Empty, new Dictionary<string, string>
            {
                ["collection"] = _name
            });

            return result.Matches
                .Select(match => ToEmbeddingDocument(match, includeVectors))
                .ToList();
        }

        private async Task<float[]?> TryGetQueryVectorAsync<TInput>(TInput searchValue, CancellationToken cancellationToken)
        {
            return searchValue switch
            {
                string text when !string.IsNullOrWhiteSpace(text) && _embeddingGenerator is not null
                    => (await _embeddingGenerator.GenerateAsync(text, cancellationToken: cancellationToken)).Vector.ToArray(),
                float[] vector => vector,
                ReadOnlyMemory<float> vector => vector.ToArray(),
                Embedding<float> embedding => embedding.Vector.ToArray(),
                _ => null
            };
        }

        private static double CalculateScore(string? queryText, float[]? queryVector, EmbeddingDocument document)
        {
            if (queryVector is { Length: > 0 } && document.Embedding is { Length: > 0 })
            {
                return NormalizeCosine(queryVector, document.Embedding);
            }

            if (!string.IsNullOrWhiteSpace(queryText))
            {
                return CalculateLexicalScore(queryText, document);
            }

            return 1.0;
        }

        private static double CalculateLexicalScore(string query, EmbeddingDocument document)
        {
            var queryLower = query.ToLowerInvariant();
            var contentLower = document.Content.ToLowerInvariant();
            var words = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                return 0;
            }

            var matchCount = words.Count(contentLower.Contains);
            var score = (double)matchCount / words.Length;

            if (contentLower.Contains(queryLower))
            {
                score = Math.Min(score + 0.3, 1.0);
            }

            return score;
        }

        private static double NormalizeCosine(float[] left, float[] right)
        {
            if (left.Length != right.Length)
            {
                return 0;
            }

            double dot = 0;
            double leftNorm = 0;
            double rightNorm = 0;

            for (var i = 0; i < left.Length; i++)
            {
                dot += left[i] * (double)right[i];
                leftNorm += left[i] * (double)left[i];
                rightNorm += right[i] * (double)right[i];
            }

            var denominator = Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm);
            if (denominator == 0)
            {
                return 0;
            }

            return (dot / denominator + 1d) / 2d;
        }

        private static EmbeddingDocument ToEmbeddingDocument(SearchMatch match, bool includeVectors)
        {
            return new EmbeddingDocument
            {
                Id = match.Id,
                Content = match.Content,
                Type = match.Type,
                Collection = match.Collection,
                Embedding = includeVectors ? match.Embedding ?? Array.Empty<float>() : Array.Empty<float>(),
                Metadata = match.Metadata,
                IndexedAt = match.IndexedAt
            };
        }

        private static EmbeddingDocument WithoutVectors(EmbeddingDocument document)
        {
            return new EmbeddingDocument
            {
                Id = document.Id,
                Content = document.Content,
                Type = document.Type,
                Collection = document.Collection,
                Embedding = Array.Empty<float>(),
                Metadata = document.Metadata,
                IndexedAt = document.IndexedAt
            };
        }
    }

    private sealed class AgenticDynamicCollection : VectorStoreCollection<object, Dictionary<string, object?>>
    {
        private readonly AgenticEmbeddingCollection _inner;
        private readonly VectorStoreCollectionMetadata _metadata;

        public AgenticDynamicCollection(
            string name,
            VectorStoreCollectionDefinition definition,
            IVectorStore store,
            TextEmbeddingGenerator? embeddingGenerator,
            ConcurrentDictionary<string, byte> managedCollections)
        {
            _inner = new AgenticEmbeddingCollection(name, definition, store, embeddingGenerator, managedCollections);
            _metadata = new VectorStoreCollectionMetadata
            {
                VectorStoreSystemName = s_storeMetadata.VectorStoreSystemName,
                VectorStoreName = s_storeMetadata.VectorStoreName,
                CollectionName = name
            };
        }

        public override string Name => _inner.Name;

        public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
            => _inner.CollectionExistsAsync(cancellationToken);

        public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
            => _inner.EnsureCollectionExistsAsync(cancellationToken);

        public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
            => _inner.EnsureCollectionDeletedAsync(cancellationToken);

        public override async Task<Dictionary<string, object?>?> GetAsync(object key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
        {
            var record = await _inner.GetAsync(key.ToString() ?? string.Empty, options, cancellationToken);
            return record is null ? null : ToDictionary(record, options?.IncludeVectors ?? false);
        }

        public override async IAsyncEnumerable<Dictionary<string, object?>> GetAsync(
            Expression<Func<Dictionary<string, object?>, bool>> filter,
            int top,
            FilteredRecordRetrievalOptions<Dictionary<string, object?>>? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var predicate = filter.Compile();
            var includeVectors = options?.IncludeVectors ?? false;

            await foreach (var record in _inner.GetAsync(_ => true, int.MaxValue, new FilteredRecordRetrievalOptions<EmbeddingDocument> { IncludeVectors = true }, cancellationToken))
            {
                var dictionary = ToDictionary(record, includeVectors: true);
                if (!predicate(dictionary))
                {
                    continue;
                }

                yield return includeVectors ? dictionary : WithoutVectors(dictionary);
                if (--top == 0)
                {
                    yield break;
                }
            }
        }

        public override Task DeleteAsync(object key, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(key.ToString() ?? string.Empty, cancellationToken);

        public override Task UpsertAsync(Dictionary<string, object?> record, CancellationToken cancellationToken = default)
        {
            return _inner.UpsertAsync(ToEmbeddingDocument(record), cancellationToken);
        }

        public override async Task UpsertAsync(IEnumerable<Dictionary<string, object?>> records, CancellationToken cancellationToken = default)
        {
            foreach (var record in records)
            {
                await UpsertAsync(record, cancellationToken);
            }
        }

        public override async IAsyncEnumerable<VectorSearchResult<Dictionary<string, object?>>> SearchAsync<TInput>(
            TInput searchValue,
            int top,
            VectorSearchOptions<Dictionary<string, object?>>? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var filter = options?.Filter?.Compile();
            var includeVectors = options?.IncludeVectors ?? false;
            var skipped = 0;
            var yielded = 0;

            await foreach (var result in _inner.SearchAsync(
                searchValue,
                int.MaxValue,
                new VectorSearchOptions<EmbeddingDocument>
                {
                    IncludeVectors = true
                },
                cancellationToken))
            {
                if (options?.ScoreThreshold is { } threshold && result.Score < threshold)
                {
                    continue;
                }

                var dictionary = ToDictionary(result.Record, includeVectors: true);
                if (filter is not null && !filter(dictionary))
                {
                    continue;
                }

                if (skipped < (options?.Skip ?? 0))
                {
                    skipped++;
                    continue;
                }

                yield return new VectorSearchResult<Dictionary<string, object?>>(ToDictionary(result.Record, includeVectors), result.Score);
                yielded++;

                if (yielded >= top)
                {
                    yield break;
                }
            }
        }

        public override object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceType == typeof(VectorStoreCollectionMetadata))
                return _metadata;
            return _inner.GetService(serviceType, serviceKey);
        }

        private static Dictionary<string, object?> ToDictionary(EmbeddingDocument document, bool includeVectors)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = document.Id,
                ["content"] = document.Content,
                ["type"] = document.Type,
                ["collection"] = document.Collection,
                ["indexedAt"] = document.IndexedAt,
                ["metadata"] = document.Metadata,
                ["embedding"] = includeVectors ? document.Embedding : Array.Empty<float>()
            };
        }

        private static Dictionary<string, object?> WithoutVectors(Dictionary<string, object?> record)
        {
            var clone = new Dictionary<string, object?>(record, StringComparer.OrdinalIgnoreCase);
            clone["embedding"] = Array.Empty<float>();
            return clone;
        }

        private static EmbeddingDocument ToEmbeddingDocument(Dictionary<string, object?> record)
        {
            var metadata = record.TryGetValue("metadata", out var metadataValue) && metadataValue is Dictionary<string, string> typedMetadata
                ? typedMetadata
                : new Dictionary<string, string>();

            return new EmbeddingDocument
            {
                Id = record["id"]?.ToString() ?? Guid.NewGuid().ToString("N"),
                Content = record["content"]?.ToString() ?? string.Empty,
                Type = record["type"]?.ToString() ?? string.Empty,
                Collection = record["collection"]?.ToString() ?? string.Empty,
                Metadata = metadata,
                IndexedAt = record.TryGetValue("indexedAt", out var indexedAtValue) && indexedAtValue is DateTime indexedAt
                    ? indexedAt
                    : DateTime.UtcNow,
                Embedding = record.TryGetValue("embedding", out var embeddingValue)
                    ? embeddingValue switch
                    {
                        float[] vector => vector,
                        ReadOnlyMemory<float> vector => vector.ToArray(),
                        _ => Array.Empty<float>()
                    }
                    : Array.Empty<float>()
            };
        }
    }
}
