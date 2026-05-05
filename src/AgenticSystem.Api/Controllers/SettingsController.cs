using System.IO.Compression;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.RAG;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/settings")]
public class SettingsController : ControllerBase
{
    private readonly AgenticSystemSettings _settings;
    private readonly IRerankingSettingsAccessor _rerankingSettingsAccessor;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        IOptions<AgenticSystemSettings> settings,
        IRerankingSettingsAccessor rerankingSettingsAccessor,
        ILogger<SettingsController> logger)
    {
        _settings = settings.Value;
        _rerankingSettingsAccessor = rerankingSettingsAccessor;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var rerankingState = await _rerankingSettingsAccessor.GetCurrentStateAsync(ct);

        return Ok(new
        {
            openAI = new { _settings.OpenAI.BaseUrl, _settings.OpenAI.DefaultModel, _settings.OpenAI.Enabled, _settings.OpenAI.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.OpenAI.ApiKey) },
            ollama = new { _settings.Ollama.BaseUrl, _settings.Ollama.DefaultModel, _settings.Ollama.Enabled, _settings.Ollama.Priority },
            gemini = new { _settings.Gemini.BaseUrl, _settings.Gemini.DefaultModel, _settings.Gemini.Enabled, _settings.Gemini.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.Gemini.ApiKey) },
            claude = new { _settings.Claude.BaseUrl, _settings.Claude.DefaultModel, _settings.Claude.Enabled, _settings.Claude.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.Claude.ApiKey) },
            gateway = _settings.Gateway,
            memory = _settings.Memory,
            reranking = MapReRankingSettings(rerankingState)
        });
    }

    [HttpGet("gateway")]
    public IActionResult GetGatewaySettings()
    {
        return Ok(_settings.Gateway);
    }

    [HttpGet("memory")]
    public IActionResult GetMemorySettings()
    {
        return Ok(_settings.Memory);
    }

    [HttpGet("reranking")]
    public async Task<IActionResult> GetReRankingSettings(CancellationToken ct)
    {
        var state = await _rerankingSettingsAccessor.GetCurrentStateAsync(ct);
        return Ok(MapReRankingSettings(state));
    }

    [HttpGet("providers")]
    public IActionResult GetProviderSettings()
    {
        return Ok(new
        {
            openAI = new { _settings.OpenAI.BaseUrl, _settings.OpenAI.DefaultModel, _settings.OpenAI.Enabled, _settings.OpenAI.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.OpenAI.ApiKey) },
            ollama = new { _settings.Ollama.BaseUrl, _settings.Ollama.DefaultModel, _settings.Ollama.Enabled, _settings.Ollama.Priority },
            gemini = new { _settings.Gemini.BaseUrl, _settings.Gemini.DefaultModel, _settings.Gemini.Enabled, _settings.Gemini.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.Gemini.ApiKey) },
            claude = new { _settings.Claude.BaseUrl, _settings.Claude.DefaultModel, _settings.Claude.Enabled, _settings.Claude.Priority, hasApiKey = !string.IsNullOrWhiteSpace(_settings.Claude.ApiKey) }
        });
    }

    [HttpPut("gateway")]
    public IActionResult UpdateGatewaySettings([FromBody] GatewaySettings update)
    {
        _settings.Gateway.DefaultDailyBudget = update.DefaultDailyBudget;
        _settings.Gateway.DefaultFailureThreshold = update.DefaultFailureThreshold;
        _settings.Gateway.DefaultBreakDurationSeconds = update.DefaultBreakDurationSeconds;
        _settings.Gateway.DefaultRequestsPerMinute = update.DefaultRequestsPerMinute;
        _logger.LogInformation("Gateway settings updated");
        return Ok(_settings.Gateway);
    }

    [HttpPut("memory")]
    public IActionResult UpdateMemorySettings([FromBody] MemorySettings update)
    {
        _settings.Memory.ObsidianVaultPath = update.ObsidianVaultPath;
        _settings.Memory.VectorStoreType = update.VectorStoreType;
        _settings.Memory.ConnectionString = update.ConnectionString;
        _logger.LogInformation("Memory settings updated");
        return Ok(_settings.Memory);
    }

    [HttpPut("reranking")]
    public async Task<IActionResult> UpdateReRankingSettings([FromBody] UpdateReRankingSettingsRequest update, CancellationToken ct)
    {
        var state = await _rerankingSettingsAccessor.SaveSettingsAsync(new RerankingSettingsUpdate
        {
            Enabled = update.Enabled,
            UseDedicatedProvider = update.UseDedicatedProvider,
            DedicatedProvider = update.DedicatedProvider,
            DedicatedProviderBaseUrl = update.DedicatedProviderBaseUrl,
            DedicatedProviderModel = update.DedicatedProviderModel,
            DedicatedProviderApiKey = update.DedicatedProviderApiKey,
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
        }, ct);

        _logger.LogInformation("Re-ranking settings updated");
        return Ok(MapReRankingSettings(state));
    }

    [HttpPost("reranking/assets")]
    public async Task<IActionResult> UploadReRankingAssets(
        [FromForm] IFormFile? modelFile,
        [FromForm] IFormFile? vocabularyFile,
        [FromForm] IFormFile? packageFile,
        CancellationToken ct)
    {
        if ((modelFile is null || modelFile.Length == 0)
            && (vocabularyFile is null || vocabularyFile.Length == 0)
            && (packageFile is null || packageFile.Length == 0))
        {
            return BadRequest(new { error = "Informe model.onnx, vocab.txt e/ou um pacote ZIP com os assets." });
        }

        if (modelFile is not null && !string.Equals(Path.GetExtension(modelFile.FileName), ".onnx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "O arquivo do modelo deve ter extensão .onnx." });
        }

        if (vocabularyFile is not null && !string.Equals(Path.GetExtension(vocabularyFile.FileName), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "O arquivo de vocabulário deve ter extensão .txt." });
        }

        if (packageFile is not null && !string.Equals(Path.GetExtension(packageFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "O pacote de assets deve ter extensão .zip." });
        }

        ExtractedRerankingPackage? packagedAssets = null;
        if (packageFile is not null && packageFile.Length > 0)
        {
            try
            {
                packagedAssets = await ExtractPackageAssetsAsync(packageFile, ct);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        var modelBytes = modelFile is not null && modelFile.Length > 0
            ? await ReadFileBytesAsync(modelFile, ct)
            : packagedAssets?.ModelBytes;
        var modelFileName = modelFile is not null && modelFile.Length > 0
            ? modelFile.FileName
            : packagedAssets?.ModelFileName;
        var modelContentType = modelFile is not null && modelFile.Length > 0
            ? modelFile.ContentType
            : packagedAssets?.ModelContentType;

        var vocabularyBytes = vocabularyFile is not null && vocabularyFile.Length > 0
            ? await ReadFileBytesAsync(vocabularyFile, ct)
            : packagedAssets?.VocabularyBytes;
        var vocabularyFileName = vocabularyFile is not null && vocabularyFile.Length > 0
            ? vocabularyFile.FileName
            : packagedAssets?.VocabularyFileName;
        var vocabularyContentType = vocabularyFile is not null && vocabularyFile.Length > 0
            ? vocabularyFile.ContentType
            : packagedAssets?.VocabularyContentType;

        if (modelBytes is null && vocabularyBytes is null)
        {
            return BadRequest(new { error = "O pacote ZIP deve conter pelo menos um arquivo .onnx e/ou um vocab.txt válido." });
        }

        var payload = new RerankingAssetPayload
        {
            ModelBytes = modelBytes,
            ModelFileName = modelFileName,
            ModelContentType = modelContentType,
            VocabularyBytes = vocabularyBytes,
            VocabularyFileName = vocabularyFileName,
            VocabularyContentType = vocabularyContentType
        };

        var state = await _rerankingSettingsAccessor.SaveAssetsAsync(payload, ct);
        _logger.LogInformation("Re-ranking assets uploaded");
        return Ok(MapReRankingSettings(state));
    }

    private static ReRankingSettingsResponse MapReRankingSettings(RerankingSettingsState state)
        => new()
        {
            Enabled = state.Options.Enabled,
            UseDedicatedProvider = state.Options.UseDedicatedProvider,
            DedicatedProvider = state.Options.DedicatedProvider,
            DedicatedProviderBaseUrl = state.Options.DedicatedProviderBaseUrl,
            DedicatedProviderModel = state.Options.DedicatedProviderModel,
            HasDedicatedProviderApiKey = state.HasDedicatedProviderApiKey,
            DedicatedProviderTimeoutSeconds = state.Options.DedicatedProviderTimeoutSeconds,
            LocalOnnxModelPath = state.Options.LocalOnnxModelPath,
            LocalOnnxVocabularyPath = state.Options.LocalOnnxVocabularyPath,
            LocalOnnxMaxSequenceLength = state.Options.LocalOnnxMaxSequenceLength,
            LocalOnnxMaxQueryTokens = state.Options.LocalOnnxMaxQueryTokens,
            LocalOnnxLowerCase = state.Options.LocalOnnxLowerCase,
            LocalOnnxInputIdsName = state.Options.LocalOnnxInputIdsName,
            LocalOnnxAttentionMaskName = state.Options.LocalOnnxAttentionMaskName,
            LocalOnnxTokenTypeIdsName = state.Options.LocalOnnxTokenTypeIdsName,
            LocalOnnxOutputName = state.Options.LocalOnnxOutputName,
            LocalOnnxPositiveLabelIndex = state.Options.LocalOnnxPositiveLabelIndex,
            UseEmbeddingReRanking = state.Options.UseEmbeddingReRanking,
            UseLlmReRanking = state.Options.UseLlmReRanking,
            CandidatePoolSize = state.Options.CandidatePoolSize,
            MinCandidateCountForLlm = state.Options.MinCandidateCountForLlm,
            MaxSnippetCharacters = state.Options.MaxSnippetCharacters,
            MaxOutputTokens = state.Options.MaxOutputTokens,
            Temperature = state.Options.Temperature,
            HeuristicConfidenceThreshold = state.Options.HeuristicConfidenceThreshold,
            HeuristicConfidenceGap = state.Options.HeuristicConfidenceGap,
            NeuralScoreWeight = state.Options.NeuralScoreWeight,
            LlmScoreWeight = state.Options.LlmScoreWeight,
            HasUploadedLocalOnnxModel = state.HasUploadedLocalOnnxModel,
            UploadedLocalOnnxModelFileName = state.UploadedLocalOnnxModelFileName,
            HasUploadedLocalOnnxVocabulary = state.HasUploadedLocalOnnxVocabulary,
            UploadedLocalOnnxVocabularyFileName = state.UploadedLocalOnnxVocabularyFileName
        };

    private static async Task<byte[]?> ReadFileBytesAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        await using var stream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, ct);
        return memoryStream.ToArray();
    }

    private static async Task<ExtractedRerankingPackage> ExtractPackageAssetsAsync(IFormFile packageFile, CancellationToken ct)
    {
        await using var packageStream = packageFile.OpenReadStream();
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);

        var modelEntry = archive.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Name, "model.onnx", StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault(entry =>
                string.Equals(Path.GetExtension(entry.Name), ".onnx", StringComparison.OrdinalIgnoreCase));

        var vocabularyEntry = archive.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Name, "vocab.txt", StringComparison.OrdinalIgnoreCase))
            ?? archive.Entries.FirstOrDefault(entry =>
                string.Equals(entry.Name, "vocab.txt", StringComparison.OrdinalIgnoreCase));

        if (modelEntry is null && vocabularyEntry is null)
        {
            throw new InvalidDataException("O pacote ZIP não contém model.onnx nem vocab.txt.");
        }

        return new ExtractedRerankingPackage
        {
            ModelBytes = await ReadArchiveEntryBytesAsync(modelEntry, ct),
            ModelFileName = modelEntry?.Name,
            ModelContentType = modelEntry is null ? null : "application/octet-stream",
            VocabularyBytes = await ReadArchiveEntryBytesAsync(vocabularyEntry, ct),
            VocabularyFileName = vocabularyEntry?.Name,
            VocabularyContentType = vocabularyEntry is null ? null : "text/plain"
        };
    }

    private static async Task<byte[]?> ReadArchiveEntryBytesAsync(ZipArchiveEntry? entry, CancellationToken ct)
    {
        if (entry is null)
        {
            return null;
        }

        await using var entryStream = entry.Open();
        using var memoryStream = new MemoryStream();
        await entryStream.CopyToAsync(memoryStream, ct);
        return memoryStream.ToArray();
    }

    private sealed class ExtractedRerankingPackage
    {
        public byte[]? ModelBytes { get; init; }
        public string? ModelFileName { get; init; }
        public string? ModelContentType { get; init; }
        public byte[]? VocabularyBytes { get; init; }
        public string? VocabularyFileName { get; init; }
        public string? VocabularyContentType { get; init; }
    }
}

public class ReRankingSettingsResponse
{
    public bool Enabled { get; set; }
    public bool UseDedicatedProvider { get; set; }
    public string DedicatedProvider { get; set; } = string.Empty;
    public string DedicatedProviderBaseUrl { get; set; } = string.Empty;
    public string DedicatedProviderModel { get; set; } = string.Empty;
    public bool HasDedicatedProviderApiKey { get; set; }
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
    public bool HasUploadedLocalOnnxModel { get; set; }
    public string? UploadedLocalOnnxModelFileName { get; set; }
    public bool HasUploadedLocalOnnxVocabulary { get; set; }
    public string? UploadedLocalOnnxVocabularyFileName { get; set; }
}

public sealed class UpdateReRankingSettingsRequest : ReRankingSettingsResponse
{
    public string? DedicatedProviderApiKey { get; set; }
}
