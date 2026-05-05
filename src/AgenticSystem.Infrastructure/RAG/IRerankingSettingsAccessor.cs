using AgenticSystem.Infrastructure.Configuration;

namespace AgenticSystem.Infrastructure.RAG;

public interface IRerankingSettingsAccessor
{
    Task<ReRankingOptions> GetCurrentOptionsAsync(CancellationToken ct = default);
    Task<RerankingSettingsState> GetCurrentStateAsync(CancellationToken ct = default);
    Task<RerankingSettingsState> SaveSettingsAsync(RerankingSettingsUpdate update, CancellationToken ct = default);
    Task<RerankingSettingsState> SaveAssetsAsync(RerankingAssetPayload payload, CancellationToken ct = default);
}

public sealed class RerankingSettingsUpdate
{
    public bool Enabled { get; set; }
    public bool UseDedicatedProvider { get; set; }
    public string DedicatedProvider { get; set; } = string.Empty;
    public string DedicatedProviderBaseUrl { get; set; } = string.Empty;
    public string DedicatedProviderModel { get; set; } = string.Empty;
    public string? DedicatedProviderApiKey { get; set; }
    public int DedicatedProviderTimeoutSeconds { get; set; }
    public string? LocalOnnxModelPath { get; set; }
    public string? LocalOnnxVocabularyPath { get; set; }
    public int LocalOnnxMaxSequenceLength { get; set; }
    public int LocalOnnxMaxQueryTokens { get; set; }
    public bool LocalOnnxLowerCase { get; set; }
    public string LocalOnnxInputIdsName { get; set; } = string.Empty;
    public string LocalOnnxAttentionMaskName { get; set; } = string.Empty;
    public string LocalOnnxTokenTypeIdsName { get; set; } = string.Empty;
    public string LocalOnnxOutputName { get; set; } = string.Empty;
    public int LocalOnnxPositiveLabelIndex { get; set; }
    public bool UseEmbeddingReRanking { get; set; }
    public bool UseLlmReRanking { get; set; }
    public int CandidatePoolSize { get; set; }
    public int MinCandidateCountForLlm { get; set; }
    public int MaxSnippetCharacters { get; set; }
    public int MaxOutputTokens { get; set; }
    public float Temperature { get; set; }
    public double HeuristicConfidenceThreshold { get; set; }
    public double HeuristicConfidenceGap { get; set; }
    public double NeuralScoreWeight { get; set; }
    public double LlmScoreWeight { get; set; }
}

public sealed class RerankingSettingsState
{
    public ReRankingOptions Options { get; init; } = new();
    public bool HasDedicatedProviderApiKey { get; init; }
    public bool HasUploadedLocalOnnxModel { get; init; }
    public string? UploadedLocalOnnxModelFileName { get; init; }
    public bool HasUploadedLocalOnnxVocabulary { get; init; }
    public string? UploadedLocalOnnxVocabularyFileName { get; init; }
}

public sealed class RerankingAssetPayload
{
    public byte[]? ModelBytes { get; init; }
    public string? ModelFileName { get; init; }
    public string? ModelContentType { get; init; }
    public byte[]? VocabularyBytes { get; init; }
    public string? VocabularyFileName { get; init; }
    public string? VocabularyContentType { get; init; }
}