using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class QueryCompressorService : IQueryCompressor
{
    private readonly ILogger<QueryCompressorService> _logger;
    private readonly ConcurrentBag<CompressedQuery> _history = new();

    // Stopwords comuns em queries de busca (pt-br + en)
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "o", "a", "os", "as", "um", "uma", "uns", "umas",
        "de", "do", "da", "dos", "das", "em", "no", "na", "nos", "nas",
        "por", "para", "com", "sem", "que", "se", "como", "mais",
        "the", "a", "an", "is", "are", "was", "were", "be", "been",
        "of", "in", "to", "for", "with", "on", "at", "from", "by",
        "and", "or", "but", "not", "this", "that", "it", "its",
        "me", "eu", "ele", "ela", "nós", "eles", "elas",
        "muito", "pode", "quero", "preciso", "gostaria", "favor",
        "please", "want", "need", "would", "could", "should"
    };

    public QueryCompressorService(ILogger<QueryCompressorService> logger)
    {
        _logger = logger;
    }

    public Task<CompressedQuery> CompressAsync(string query, QueryCompressionStrategy strategy = QueryCompressionStrategy.HybridCompression)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(new CompressedQuery
            {
                OriginalQuery = query ?? string.Empty,
                CompressedText = string.Empty,
                StrategyUsed = strategy,
                CompressionRatio = 1.0,
                ConfidenceScore = 0.0
            });
        }

        var result = strategy switch
        {
            QueryCompressionStrategy.None => PassThrough(query),
            QueryCompressionStrategy.RemoveRedundancy => RemoveRedundancy(query),
            QueryCompressionStrategy.ExtractKeyTerms => ExtractKeyTerms(query),
            QueryCompressionStrategy.SemanticNormalization => SemanticNormalize(query),
            QueryCompressionStrategy.HybridCompression => HybridCompress(query),
            _ => PassThrough(query)
        };

        result.StrategyUsed = strategy;
        _history.Add(result);

        _logger.LogDebug(
            "🗜️ Query compressed: {Original} → {Compressed} (ratio={Ratio:F2}, strategy={Strategy})",
            TruncateForLog(query), TruncateForLog(result.CompressedText),
            result.CompressionRatio, strategy);

        return Task.FromResult(result);
    }

    public Task<CompressedQuery> CompressWithContextAsync(string query, AnalysisResult? analysisContext = null)
    {
        var strategy = ResolveStrategy(query, analysisContext);
        var result = HybridCompress(query);

        // Enrich with analysis context
        if (analysisContext != null)
        {
            if (!string.IsNullOrEmpty(analysisContext.PrimaryDomain))
            {
                result.NormalizedIntent = $"{analysisContext.Intent}:{analysisContext.PrimaryDomain}";
            }

            // Add domain terms as key terms if not already present
            foreach (var domain in analysisContext.SecondaryDomains)
            {
                if (!result.ExtractedKeyTerms.Contains(domain, StringComparer.OrdinalIgnoreCase))
                {
                    result.ExtractedKeyTerms.Add(domain);
                }
            }
        }

        result.StrategyUsed = strategy;
        _history.Add(result);

        return Task.FromResult(result);
    }

    public QueryCompressionMetrics GetMetrics()
    {
        var items = _history.ToArray();
        if (items.Length == 0)
        {
            return new QueryCompressionMetrics();
        }

        return new QueryCompressionMetrics
        {
            TotalQueriesCompressed = items.Length,
            AverageCompressionRatio = items.Average(x => x.CompressionRatio),
            AverageConfidenceScore = items.Average(x => x.ConfidenceScore),
            RedundanciesRemoved = items.Sum(x => x.RemovedRedundancies.Count),
            StrategyUsage = items
                .GroupBy(x => x.StrategyUsed)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private static CompressedQuery PassThrough(string query)
    {
        var tokens = EstimateTokens(query);
        return new CompressedQuery
        {
            OriginalQuery = query,
            CompressedText = query,
            OriginalTokenCount = tokens,
            CompressedTokenCount = tokens,
            CompressionRatio = 1.0,
            ConfidenceScore = 1.0
        };
    }

    private static CompressedQuery RemoveRedundancy(string query)
    {
        var originalTokens = EstimateTokens(query);
        var words = Tokenize(query);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<string>();
        var removed = new List<string>();

        foreach (var word in words)
        {
            var normalized = word.ToLowerInvariant().Trim();
            if (seen.Add(normalized))
            {
                unique.Add(word);
            }
            else
            {
                removed.Add(word);
            }
        }

        // Also remove consecutive duplicate phrases (2-grams)
        var compressed = string.Join(" ", unique);
        compressed = RemoveRepeatedPhrases(compressed);

        var compressedTokens = EstimateTokens(compressed);

        return new CompressedQuery
        {
            OriginalQuery = query,
            CompressedText = compressed,
            RemovedRedundancies = removed,
            OriginalTokenCount = originalTokens,
            CompressedTokenCount = compressedTokens,
            CompressionRatio = originalTokens > 0 ? (double)compressedTokens / originalTokens : 1.0,
            ConfidenceScore = removed.Count > 0 ? 0.85 : 1.0
        };
    }

    private static CompressedQuery ExtractKeyTerms(string query)
    {
        var originalTokens = EstimateTokens(query);
        var words = Tokenize(query);
        var keyTerms = new List<string>();
        var removed = new List<string>();

        foreach (var word in words)
        {
            if (word.Length <= 2 || Stopwords.Contains(word))
            {
                removed.Add(word);
            }
            else
            {
                keyTerms.Add(word);
            }
        }

        // Deduplicate key terms
        keyTerms = keyTerms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var compressed = string.Join(" ", keyTerms);
        var compressedTokens = EstimateTokens(compressed);

        return new CompressedQuery
        {
            OriginalQuery = query,
            CompressedText = compressed,
            ExtractedKeyTerms = keyTerms,
            RemovedRedundancies = removed,
            OriginalTokenCount = originalTokens,
            CompressedTokenCount = compressedTokens,
            CompressionRatio = originalTokens > 0 ? (double)compressedTokens / originalTokens : 1.0,
            ConfidenceScore = 0.8
        };
    }

    private static CompressedQuery SemanticNormalize(string query)
    {
        var originalTokens = EstimateTokens(query);

        // Normalize whitespace
        var normalized = Regex.Replace(query.Trim(), @"\s+", " ");

        // Normalize punctuation
        normalized = Regex.Replace(normalized, @"[!?]{2,}", "?");

        // Remove filler phrases
        var fillers = new[]
        {
            "por favor", "please", "eu gostaria de", "i would like to",
            "você pode", "can you", "could you", "poderia",
            "me diga", "tell me", "me fala", "me explica"
        };

        foreach (var filler in fillers)
        {
            normalized = Regex.Replace(normalized, $@"\b{Regex.Escape(filler)}\b", "", RegexOptions.IgnoreCase);
        }

        normalized = Regex.Replace(normalized.Trim(), @"\s+", " ").Trim();

        // Extract intent
        var intent = InferIntent(query);

        var compressedTokens = EstimateTokens(normalized);

        return new CompressedQuery
        {
            OriginalQuery = query,
            CompressedText = normalized,
            NormalizedIntent = intent,
            OriginalTokenCount = originalTokens,
            CompressedTokenCount = compressedTokens,
            CompressionRatio = originalTokens > 0 ? (double)compressedTokens / originalTokens : 1.0,
            ConfidenceScore = 0.9
        };
    }

    private static CompressedQuery HybridCompress(string query)
    {
        // Step 1: Semantic normalization (remove fillers, normalize whitespace)
        var normalized = SemanticNormalize(query);

        // Step 2: Remove redundancy from normalized result
        var deduped = RemoveRedundancy(normalized.CompressedText);

        // Step 3: Extract key terms for search enrichment
        var keyTerms = Tokenize(deduped.CompressedText)
            .Where(w => w.Length > 2 && !Stopwords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allRemoved = normalized.RemovedRedundancies
            .Concat(deduped.RemovedRedundancies)
            .Distinct()
            .ToList();

        var originalTokens = EstimateTokens(query);
        var compressedTokens = EstimateTokens(deduped.CompressedText);

        return new CompressedQuery
        {
            OriginalQuery = query,
            CompressedText = deduped.CompressedText,
            ExtractedKeyTerms = keyTerms,
            RemovedRedundancies = allRemoved,
            NormalizedIntent = normalized.NormalizedIntent,
            OriginalTokenCount = originalTokens,
            CompressedTokenCount = compressedTokens,
            CompressionRatio = originalTokens > 0 ? (double)compressedTokens / originalTokens : 1.0,
            ConfidenceScore = 0.85
        };
    }

    private static QueryCompressionStrategy ResolveStrategy(string query, AnalysisResult? context)
    {
        if (context?.Complexity == ComplexityLevel.Simple)
            return QueryCompressionStrategy.RemoveRedundancy;

        if (EstimateTokens(query) > 50)
            return QueryCompressionStrategy.HybridCompression;

        return QueryCompressionStrategy.SemanticNormalization;
    }

    private static string InferIntent(string query)
    {
        var lower = query.ToLowerInvariant();

        if (lower.Contains("como") || lower.Contains("how"))
            return "howto";
        if (lower.Contains("o que") || lower.Contains("what is") || lower.Contains("qual"))
            return "definition";
        if (lower.Contains("por que") || lower.Contains("why"))
            return "reasoning";
        if (lower.Contains("listar") || lower.Contains("list") || lower.Contains("quais"))
            return "enumeration";
        if (lower.Contains("criar") || lower.Contains("create") || lower.Contains("gerar"))
            return "creation";
        if (lower.Contains("corrigir") || lower.Contains("fix") || lower.Contains("resolver"))
            return "troubleshooting";

        return "general";
    }

    private static List<string> Tokenize(string text)
        => Regex.Split(text, @"\s+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();

    private static string RemoveRepeatedPhrases(string text)
    {
        // Remove consecutive repeated words
        return Regex.Replace(text, @"\b(\w+)\s+\1\b", "$1", RegexOptions.IgnoreCase);
    }

    private static int EstimateTokens(string text)
        => string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);

    private static string TruncateForLog(string text)
        => text.Length > 60 ? text[..60] + "..." : text;
}
