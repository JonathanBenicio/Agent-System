namespace AgenticSystem.Infrastructure.Configuration;

public sealed class ReRankingOptions
{
    public bool Enabled { get; set; } = true;
    public bool UseDedicatedProvider { get; set; } = true;
    public string DedicatedProvider { get; set; } = "LocalOnnxCrossEncoder";
    public string DedicatedProviderBaseUrl { get; set; } = "https://api.jina.ai/v1/rerank";
    public string DedicatedProviderModel { get; set; } = "jina-reranker-v2-base-multilingual";
    public string? DedicatedProviderApiKey { get; set; }
    public int DedicatedProviderTimeoutSeconds { get; set; } = 20;
    public string? LocalOnnxModelPath { get; set; }
    public string? LocalOnnxVocabularyPath { get; set; }
    public int LocalOnnxMaxSequenceLength { get; set; } = 384;
    public int LocalOnnxMaxQueryTokens { get; set; } = 96;
    public bool LocalOnnxLowerCase { get; set; } = true;
    public string LocalOnnxInputIdsName { get; set; } = "input_ids";
    public string LocalOnnxAttentionMaskName { get; set; } = "attention_mask";
    public string LocalOnnxTokenTypeIdsName { get; set; } = "token_type_ids";
    public string LocalOnnxOutputName { get; set; } = "logits";
    public int LocalOnnxPositiveLabelIndex { get; set; } = 1;
    public bool UseEmbeddingReRanking { get; set; } = true;
    public bool UseLlmReRanking { get; set; } = false;
    public int CandidatePoolSize { get; set; } = 8;
    public int MinCandidateCountForLlm { get; set; } = 3;
    public int MaxSnippetCharacters { get; set; } = 420;
    public int MaxOutputTokens { get; set; } = 220;
    public float Temperature { get; set; } = 0.1f;
    public double HeuristicConfidenceThreshold { get; set; } = 0.9;
    public double HeuristicConfidenceGap { get; set; } = 0.15;
    public double NeuralScoreWeight { get; set; } = 0.65;
    public double LlmScoreWeight { get; set; } = 0.65;
}