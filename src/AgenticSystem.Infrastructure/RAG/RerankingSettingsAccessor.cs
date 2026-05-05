using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.RAG;

public sealed class RerankingSettingsAccessor : IRerankingSettingsAccessor
{
    private const string SettingsKeySuffix = "reranking.settings";
    private const string ApiKeySuffix = "reranking.dedicatedProviderApiKey";

    private readonly IOptionsMonitor<ReRankingOptions> _optionsMonitor;
    private readonly IConfigManager _configManager;
    private readonly IRerankingAssetStore _assetStore;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILogger<RerankingSettingsAccessor> _logger;

    public RerankingSettingsAccessor(
        IOptionsMonitor<ReRankingOptions> optionsMonitor,
        IConfigManager configManager,
        IRerankingAssetStore assetStore,
        ITenantContextAccessor tenantContextAccessor,
        ILogger<RerankingSettingsAccessor> logger)
    {
        _optionsMonitor = optionsMonitor;
        _configManager = configManager;
        _assetStore = assetStore;
        _tenantContextAccessor = tenantContextAccessor;
        _logger = logger;
    }

    public async Task<ReRankingOptions> GetCurrentOptionsAsync(CancellationToken ct = default)
    {
        var tenantId = ResolveTenantId();
        var options = await LoadTenantOptionsAsync(tenantId, ct);

        var modelAsset = await _assetStore.GetAsync(tenantId, RerankingAssetTypes.Model, ct);
        if (modelAsset is not null)
        {
            options.LocalOnnxModelPath = MaterializeAsset(tenantId, modelAsset, ".onnx");
        }

        var vocabularyAsset = await _assetStore.GetAsync(tenantId, RerankingAssetTypes.Vocabulary, ct);
        if (vocabularyAsset is not null)
        {
            options.LocalOnnxVocabularyPath = MaterializeAsset(tenantId, vocabularyAsset, ".txt");
        }

        return options;
    }

    public async Task<RerankingSettingsState> GetCurrentStateAsync(CancellationToken ct = default)
    {
        var tenantId = ResolveTenantId();
        var options = await LoadTenantOptionsAsync(tenantId, ct);
        var modelAsset = await _assetStore.GetAsync(tenantId, RerankingAssetTypes.Model, ct);
        var vocabularyAsset = await _assetStore.GetAsync(tenantId, RerankingAssetTypes.Vocabulary, ct);

        return new RerankingSettingsState
        {
            Options = options,
            HasDedicatedProviderApiKey = !string.IsNullOrWhiteSpace(options.DedicatedProviderApiKey),
            HasUploadedLocalOnnxModel = modelAsset is not null,
            UploadedLocalOnnxModelFileName = modelAsset?.FileName,
            HasUploadedLocalOnnxVocabulary = vocabularyAsset is not null,
            UploadedLocalOnnxVocabularyFileName = vocabularyAsset?.FileName
        };
    }

    public async Task<RerankingSettingsState> SaveSettingsAsync(RerankingSettingsUpdate update, CancellationToken ct = default)
    {
        var tenantId = ResolveTenantId();
        var persistedSettings = new PersistedRerankingSettings
        {
            Enabled = update.Enabled,
            UseDedicatedProvider = update.UseDedicatedProvider,
            DedicatedProvider = update.DedicatedProvider,
            DedicatedProviderBaseUrl = update.DedicatedProviderBaseUrl,
            DedicatedProviderModel = update.DedicatedProviderModel,
            DedicatedProviderTimeoutSeconds = update.DedicatedProviderTimeoutSeconds,
            LocalOnnxModelPath = update.LocalOnnxModelPath,
            LocalOnnxVocabularyPath = update.LocalOnnxVocabularyPath,
            LocalOnnxMaxSequenceLength = update.LocalOnnxMaxSequenceLength,
            LocalOnnxMaxQueryTokens = update.LocalOnnxMaxQueryTokens,
            LocalOnnxLowerCase = update.LocalOnnxLowerCase,
            LocalOnnxInputIdsName = update.LocalOnnxInputIdsName,
            LocalOnnxAttentionMaskName = update.LocalOnnxAttentionMaskName,
            LocalOnnxTokenTypeIdsName = update.LocalOnnxTokenTypeIdsName,
            LocalOnnxOutputName = update.LocalOnnxOutputName,
            LocalOnnxPositiveLabelIndex = update.LocalOnnxPositiveLabelIndex,
            UseEmbeddingReRanking = update.UseEmbeddingReRanking,
            UseLlmReRanking = update.UseLlmReRanking,
            CandidatePoolSize = update.CandidatePoolSize,
            MinCandidateCountForLlm = update.MinCandidateCountForLlm,
            MaxSnippetCharacters = update.MaxSnippetCharacters,
            MaxOutputTokens = update.MaxOutputTokens,
            Temperature = update.Temperature,
            HeuristicConfidenceThreshold = update.HeuristicConfidenceThreshold,
            HeuristicConfidenceGap = update.HeuristicConfidenceGap,
            NeuralScoreWeight = update.NeuralScoreWeight,
            LlmScoreWeight = update.LlmScoreWeight
        };

        await UpsertConfigAsync(
            BuildTenantConfigKey(tenantId, SettingsKeySuffix),
            JsonSerializer.Serialize(persistedSettings),
            isSecret: false,
            ConfigCategory.General,
            description: $"Configuração de rerank do tenant {tenantId}.");

        if (!string.IsNullOrWhiteSpace(update.DedicatedProviderApiKey))
        {
            await UpsertConfigAsync(
                BuildTenantConfigKey(tenantId, ApiKeySuffix),
                update.DedicatedProviderApiKey,
                isSecret: true,
                ConfigCategory.Credentials,
                description: $"API key do provider dedicado de rerank do tenant {tenantId}.");
        }

        _logger.LogInformation("Rerank settings persisted for tenant {TenantId}", tenantId);
        return await GetCurrentStateAsync(ct);
    }

    public async Task<RerankingSettingsState> SaveAssetsAsync(RerankingAssetPayload payload, CancellationToken ct = default)
    {
        var tenantId = ResolveTenantId();

        if (payload.ModelBytes is not null && payload.ModelBytes.Length > 0)
        {
            await _assetStore.SaveAsync(new RerankingAssetUpload
            {
                TenantId = tenantId,
                AssetType = RerankingAssetTypes.Model,
                FileName = string.IsNullOrWhiteSpace(payload.ModelFileName) ? "model.onnx" : payload.ModelFileName,
                ContentType = string.IsNullOrWhiteSpace(payload.ModelContentType) ? "application/octet-stream" : payload.ModelContentType,
                Content = payload.ModelBytes
            }, ct);
        }

        if (payload.VocabularyBytes is not null && payload.VocabularyBytes.Length > 0)
        {
            await _assetStore.SaveAsync(new RerankingAssetUpload
            {
                TenantId = tenantId,
                AssetType = RerankingAssetTypes.Vocabulary,
                FileName = string.IsNullOrWhiteSpace(payload.VocabularyFileName) ? "vocab.txt" : payload.VocabularyFileName,
                ContentType = string.IsNullOrWhiteSpace(payload.VocabularyContentType) ? "text/plain" : payload.VocabularyContentType,
                Content = payload.VocabularyBytes
            }, ct);
        }

        _logger.LogInformation("Rerank assets persisted for tenant {TenantId}", tenantId);
        return await GetCurrentStateAsync(ct);
    }

    private async Task<ReRankingOptions> LoadTenantOptionsAsync(string tenantId, CancellationToken ct)
    {
        var options = CloneOptions(_optionsMonitor.CurrentValue);

        var settingsJson = await _configManager.ResolveValueAsync(BuildTenantConfigKey(tenantId, SettingsKeySuffix));
        if (!string.IsNullOrWhiteSpace(settingsJson))
        {
            try
            {
                var persisted = JsonSerializer.Deserialize<PersistedRerankingSettings>(settingsJson);
                if (persisted is not null)
                {
                    ApplyPersistedSettings(options, persisted);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize rerank settings for tenant {TenantId}", tenantId);
            }
        }

        var apiKey = await _configManager.ResolveValueAsync(BuildTenantConfigKey(tenantId, ApiKeySuffix));
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            options.DedicatedProviderApiKey = apiKey;
        }

        return options;
    }

    private async Task UpsertConfigAsync(
        string key,
        string value,
        bool isSecret,
        ConfigCategory category,
        string description)
    {
        var request = new ConfigEntryRequest
        {
            Key = key,
            Value = value,
            IsSecret = isSecret,
            Category = category,
            Description = description,
            Provider = "reranking"
        };

        try
        {
            await _configManager.GetAsync(key);
            await _configManager.UpdateAsync(key, request);
        }
        catch (KeyNotFoundException)
        {
            await _configManager.SetAsync(request);
        }
    }

    private string ResolveTenantId()
    {
        var tenantId = _tenantContextAccessor.Current.TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? Tenant.DefaultTenantId : tenantId;
    }

    private static string BuildTenantConfigKey(string tenantId, string suffix)
        => $"tenants.{tenantId}.{suffix}";

    private static void ApplyPersistedSettings(ReRankingOptions target, PersistedRerankingSettings source)
    {
        target.Enabled = source.Enabled;
        target.UseDedicatedProvider = source.UseDedicatedProvider;
        target.DedicatedProvider = source.DedicatedProvider;
        target.DedicatedProviderBaseUrl = source.DedicatedProviderBaseUrl;
        target.DedicatedProviderModel = source.DedicatedProviderModel;
        target.DedicatedProviderTimeoutSeconds = source.DedicatedProviderTimeoutSeconds;
        target.LocalOnnxModelPath = source.LocalOnnxModelPath;
        target.LocalOnnxVocabularyPath = source.LocalOnnxVocabularyPath;
        target.LocalOnnxMaxSequenceLength = source.LocalOnnxMaxSequenceLength;
        target.LocalOnnxMaxQueryTokens = source.LocalOnnxMaxQueryTokens;
        target.LocalOnnxLowerCase = source.LocalOnnxLowerCase;
        target.LocalOnnxInputIdsName = source.LocalOnnxInputIdsName;
        target.LocalOnnxAttentionMaskName = source.LocalOnnxAttentionMaskName;
        target.LocalOnnxTokenTypeIdsName = source.LocalOnnxTokenTypeIdsName;
        target.LocalOnnxOutputName = source.LocalOnnxOutputName;
        target.LocalOnnxPositiveLabelIndex = source.LocalOnnxPositiveLabelIndex;
        target.UseEmbeddingReRanking = source.UseEmbeddingReRanking;
        target.UseLlmReRanking = source.UseLlmReRanking;
        target.CandidatePoolSize = source.CandidatePoolSize;
        target.MinCandidateCountForLlm = source.MinCandidateCountForLlm;
        target.MaxSnippetCharacters = source.MaxSnippetCharacters;
        target.MaxOutputTokens = source.MaxOutputTokens;
        target.Temperature = source.Temperature;
        target.HeuristicConfidenceThreshold = source.HeuristicConfidenceThreshold;
        target.HeuristicConfidenceGap = source.HeuristicConfidenceGap;
        target.NeuralScoreWeight = source.NeuralScoreWeight;
        target.LlmScoreWeight = source.LlmScoreWeight;
    }

    private static ReRankingOptions CloneOptions(ReRankingOptions source)
        => new()
        {
            Enabled = source.Enabled,
            UseDedicatedProvider = source.UseDedicatedProvider,
            DedicatedProvider = source.DedicatedProvider,
            DedicatedProviderBaseUrl = source.DedicatedProviderBaseUrl,
            DedicatedProviderModel = source.DedicatedProviderModel,
            DedicatedProviderApiKey = source.DedicatedProviderApiKey,
            DedicatedProviderTimeoutSeconds = source.DedicatedProviderTimeoutSeconds,
            LocalOnnxModelPath = source.LocalOnnxModelPath,
            LocalOnnxVocabularyPath = source.LocalOnnxVocabularyPath,
            LocalOnnxMaxSequenceLength = source.LocalOnnxMaxSequenceLength,
            LocalOnnxMaxQueryTokens = source.LocalOnnxMaxQueryTokens,
            LocalOnnxLowerCase = source.LocalOnnxLowerCase,
            LocalOnnxInputIdsName = source.LocalOnnxInputIdsName,
            LocalOnnxAttentionMaskName = source.LocalOnnxAttentionMaskName,
            LocalOnnxTokenTypeIdsName = source.LocalOnnxTokenTypeIdsName,
            LocalOnnxOutputName = source.LocalOnnxOutputName,
            LocalOnnxPositiveLabelIndex = source.LocalOnnxPositiveLabelIndex,
            UseEmbeddingReRanking = source.UseEmbeddingReRanking,
            UseLlmReRanking = source.UseLlmReRanking,
            CandidatePoolSize = source.CandidatePoolSize,
            MinCandidateCountForLlm = source.MinCandidateCountForLlm,
            MaxSnippetCharacters = source.MaxSnippetCharacters,
            MaxOutputTokens = source.MaxOutputTokens,
            Temperature = source.Temperature,
            HeuristicConfidenceThreshold = source.HeuristicConfidenceThreshold,
            HeuristicConfidenceGap = source.HeuristicConfidenceGap,
            NeuralScoreWeight = source.NeuralScoreWeight,
            LlmScoreWeight = source.LlmScoreWeight
        };

    private static string MaterializeAsset(string tenantId, StoredRerankingAsset asset, string fallbackExtension)
    {
        var root = Path.Combine(Path.GetTempPath(), "agentic", "reranking-assets", SanitizePathSegment(tenantId), asset.ContentHash.ToLowerInvariant());
        Directory.CreateDirectory(root);

        var extension = Path.GetExtension(asset.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = fallbackExtension;
        }

        var targetFileName = asset.AssetType == RerankingAssetTypes.Model ? $"model{extension}" : $"vocab{extension}";
        var path = Path.Combine(root, targetFileName);
        if (!File.Exists(path) || new FileInfo(path).Length != asset.Content.LongLength)
        {
            File.WriteAllBytes(path, asset.Content);
        }

        return path;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }

    private sealed class PersistedRerankingSettings
    {
        public bool Enabled { get; set; } = true;
        public bool UseDedicatedProvider { get; set; } = true;
        public string DedicatedProvider { get; set; } = "LocalOnnxCrossEncoder";
        public string DedicatedProviderBaseUrl { get; set; } = "https://api.jina.ai/v1/rerank";
        public string DedicatedProviderModel { get; set; } = "jina-reranker-v2-base-multilingual";
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
        public bool UseLlmReRanking { get; set; }
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
}