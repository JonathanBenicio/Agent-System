using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.RAG;

public sealed class JinaReRankerProvider : IDedicatedReRankerProvider
{
    private readonly HttpClient _httpClient;
    private readonly IRerankingSettingsAccessor _settingsAccessor;
    private readonly ILogger<JinaReRankerProvider> _logger;

    public JinaReRankerProvider(
        HttpClient httpClient,
        IRerankingSettingsAccessor settingsAccessor,
        ILogger<JinaReRankerProvider> logger)
    {
        _httpClient = httpClient;
        _settingsAccessor = settingsAccessor;
        _logger = logger;
    }

    public string Name => "jina-reranker";

    public async Task<DedicatedReRankingResult> ScoreAsync(
        string query,
        IReadOnlyList<RankedChunk> candidates,
        CancellationToken ct = default)
    {
        var options = await _settingsAccessor.GetCurrentOptionsAsync(ct);
        if (!IsConfigured(options) || !string.Equals(options.DedicatedProvider, "jina-reranker", StringComparison.OrdinalIgnoreCase))
        {
            return DedicatedReRankingResult.Empty;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, options.DedicatedProviderTimeoutSeconds)));
            using var request = new HttpRequestMessage(HttpMethod.Post, options.DedicatedProviderBaseUrl)
            {
                Content = JsonContent.Create(new JinaReRankRequest(
                    options.DedicatedProviderModel,
                    query,
                    candidates.Select(BuildCandidateText).ToList(),
                    candidates.Count))
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.DedicatedProviderApiKey);

            using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<JinaReRankResponse>(cancellationToken: timeoutCts.Token);
            if (payload?.Results is null || payload.Results.Count == 0)
            {
                return DedicatedReRankingResult.Empty;
            }

            var rawScores = payload.Results
                .Where(result => result.Index >= 0 && result.Index < candidates.Count)
                .Select(result => (Id: candidates[result.Index].Id, Score: result.RelevanceScore))
                .ToList();

            if (rawScores.Count == 0)
            {
                return DedicatedReRankingResult.Empty;
            }

            return new DedicatedReRankingResult(NormalizeScores(rawScores), Name);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dedicated Jina reranker failed for query '{Query}'", query);
            return DedicatedReRankingResult.Empty;
        }
    }

    private static bool IsConfigured(ReRankingOptions options)
        => options.UseDedicatedProvider
           && !string.IsNullOrWhiteSpace(options.DedicatedProviderApiKey)
           && !string.IsNullOrWhiteSpace(options.DedicatedProviderBaseUrl)
           && !string.IsNullOrWhiteSpace(options.DedicatedProviderModel);

    private string BuildCandidateText(RankedChunk candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Section))
        {
            return candidate.Content;
        }

        return $"{candidate.Section}: {candidate.Content}";
    }

    private static IReadOnlyDictionary<string, double> NormalizeScores(IReadOnlyList<(string Id, double Score)> rawScores)
    {
        var needsNormalization = rawScores.Any(item => item.Score < 0.0 || item.Score > 1.0);
        if (!needsNormalization)
        {
            return rawScores.ToDictionary(
                item => item.Id,
                item => Math.Clamp(item.Score, 0.0, 1.0),
                StringComparer.OrdinalIgnoreCase);
        }

        var min = rawScores.Min(item => item.Score);
        var max = rawScores.Max(item => item.Score);
        if (Math.Abs(max - min) < double.Epsilon)
        {
            return rawScores.ToDictionary(
                item => item.Id,
                _ => 1.0,
                StringComparer.OrdinalIgnoreCase);
        }

        return rawScores.ToDictionary(
            item => item.Id,
            item => Math.Clamp((item.Score - min) / (max - min), 0.0, 1.0),
            StringComparer.OrdinalIgnoreCase);
    }

    private sealed record JinaReRankRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("documents")] IReadOnlyList<string> Documents,
        [property: JsonPropertyName("top_n")] int TopN);

    private sealed class JinaReRankResponse
    {
        [JsonPropertyName("results")]
        public List<JinaReRankResponseItem> Results { get; init; } = [];
    }

    private sealed class JinaReRankResponseItem
    {
        [JsonPropertyName("index")]
        public int Index { get; init; }

        [JsonPropertyName("relevance_score")]
        public double RelevanceScore { get; init; }
    }
}