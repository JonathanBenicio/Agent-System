using System.Collections.Concurrent;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace AgenticSystem.Infrastructure.RAG;

public sealed class LocalOnnxCrossEncoderReRankerProvider : IDedicatedReRankerProvider, IDisposable
{
    private readonly IRerankingSettingsAccessor _settingsAccessor;
    private readonly ILogger<LocalOnnxCrossEncoderReRankerProvider> _logger;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, double>> _scoreCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<LoadedArtifacts>> _loadedArtifacts = new(StringComparer.Ordinal);

    private bool _disposed;

    public LocalOnnxCrossEncoderReRankerProvider(
        IRerankingSettingsAccessor settingsAccessor,
        ILogger<LocalOnnxCrossEncoderReRankerProvider> logger)
    {
        _settingsAccessor = settingsAccessor;
        _logger = logger;
    }

    public string Name => "LocalOnnxCrossEncoder";

    public async Task<DedicatedReRankingResult> ScoreAsync(
        string query,
        IReadOnlyList<RankedChunk> candidates,
        CancellationToken ct = default)
    {
        var options = await _settingsAccessor.GetCurrentOptionsAsync(ct);
        if (!CanRunLocally(options))
        {
            return DedicatedReRankingResult.Empty;
        }

        var signature = BuildConfigurationSignature(options);
        var loadedArtifacts = EnsureInitialized(signature, options);
        if (loadedArtifacts is null)
        {
            return DedicatedReRankingResult.Empty;
        }

        ct.ThrowIfCancellationRequested();

        var cacheKey = BuildCacheKey(signature, query, candidates);
        if (_scoreCache.TryGetValue(cacheKey, out var cached))
        {
            return new DedicatedReRankingResult(cached, Name);
        }

        var scores = ScoreCore(loadedArtifacts, query, candidates, ct, options);
        _scoreCache[cacheKey] = scores;
        return new DedicatedReRankingResult(scores, Name);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var item in _loadedArtifacts.Values)
        {
            if (item.IsValueCreated)
            {
                item.Value.Dispose();
            }
        }

        _disposed = true;
    }

    private static bool CanRunLocally(ReRankingOptions options)
    {
        if (!options.UseDedicatedProvider || !string.Equals(options.DedicatedProvider, "LocalOnnxCrossEncoder", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(options.LocalOnnxModelPath)
               && !string.IsNullOrWhiteSpace(options.LocalOnnxVocabularyPath);
    }

    private LoadedArtifacts? EnsureInitialized(string signature, ReRankingOptions options)
    {
        // Pre-check file existence to avoid creating Lazy entries for missing files
        if (string.IsNullOrWhiteSpace(options.LocalOnnxModelPath) || string.IsNullOrWhiteSpace(options.LocalOnnxVocabularyPath))
        {
            return null;
        }

        var modelPath = ResolvePath(options.LocalOnnxModelPath);
        var vocabularyPath = ResolvePath(options.LocalOnnxVocabularyPath);

        if (!File.Exists(modelPath) || !File.Exists(vocabularyPath))
        {
            _logger.LogWarning("Local ONNX reranker assets not found. Path={ModelPath}. Reranking will fallback to other providers.", modelPath);
            return null;
        }

        var lazy = _loadedArtifacts.GetOrAdd(signature, _ =>
            new Lazy<LoadedArtifacts>(() => CreateLoadedArtifacts(options), LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return lazy.Value;
        }
        catch (Exception ex)
        {
            _loadedArtifacts.TryRemove(signature, out _);
            _logger.LogError(ex, "Failed to initialize local ONNX reranker provider for signature {Signature}", signature);
            return null;
        }
    }

    private IReadOnlyDictionary<string, double> ScoreCore(
        LoadedArtifacts loadedArtifacts,
        string query,
        IReadOnlyList<RankedChunk> candidates,
        CancellationToken ct,
        ReRankingOptions options)
    {
        var maxSequenceLength = Math.Max(32, options.LocalOnnxMaxSequenceLength);
        var inputIds = new DenseTensor<long>(new[] { candidates.Count, maxSequenceLength });
        var attentionMask = new DenseTensor<long>(new[] { candidates.Count, maxSequenceLength });
        var tokenTypeIds = new DenseTensor<long>(new[] { candidates.Count, maxSequenceLength });

        for (var row = 0; row < candidates.Count; row++)
        {
            ct.ThrowIfCancellationRequested();
            PopulatePairTensors(query, candidates[row], row, inputIds, attentionMask, tokenTypeIds, maxSequenceLength, loadedArtifacts.Tokenizer, options);
        }

        using var results = loadedArtifacts.Session.Run(BuildInputs(inputIds, attentionMask, tokenTypeIds, options));
        var logitsValue = results.FirstOrDefault(item => string.Equals(item.Name, options.LocalOnnxOutputName, StringComparison.OrdinalIgnoreCase))
            ?? results.First();

        var logits = logitsValue.AsTensor<float>();
        return BuildScores(candidates, logits, options);
    }

    private void PopulatePairTensors(
        string query,
        RankedChunk candidate,
        int row,
        DenseTensor<long> inputIds,
        DenseTensor<long> attentionMask,
        DenseTensor<long> tokenTypeIds,
        int maxSequenceLength,
        BertTokenizer tokenizer,
        ReRankingOptions options)
    {
        var queryTokenIds = tokenizer.EncodeToIds(query, considerNormalization: true, considerPreTokenization: true, addSpecialTokens: false);
        var documentText = string.IsNullOrWhiteSpace(candidate.Section)
            ? candidate.Content
            : $"{candidate.Section}: {candidate.Content}";
        var documentTokenIds = tokenizer.EncodeToIds(documentText, considerNormalization: true, considerPreTokenization: true, addSpecialTokens: false);

        var maxQueryTokens = Math.Min(Math.Max(8, options.LocalOnnxMaxQueryTokens), maxSequenceLength - 3);
        if (queryTokenIds.Count > maxQueryTokens)
        {
            queryTokenIds = queryTokenIds.Take(maxQueryTokens).ToArray();
        }

        var maxDocumentTokens = Math.Max(8, maxSequenceLength - queryTokenIds.Count - 3);
        if (documentTokenIds.Count > maxDocumentTokens)
        {
            documentTokenIds = documentTokenIds.Take(maxDocumentTokens).ToArray();
        }

        var position = 0;
        WriteToken(row, ref position, tokenizer.ClassificationTokenId, segmentId: 0, inputIds, attentionMask, tokenTypeIds);

        foreach (var tokenId in queryTokenIds)
        {
            WriteToken(row, ref position, tokenId, segmentId: 0, inputIds, attentionMask, tokenTypeIds);
        }

        WriteToken(row, ref position, tokenizer.SeparatorTokenId, segmentId: 0, inputIds, attentionMask, tokenTypeIds);

        foreach (var tokenId in documentTokenIds)
        {
            WriteToken(row, ref position, tokenId, segmentId: 1, inputIds, attentionMask, tokenTypeIds);
        }

        WriteToken(row, ref position, tokenizer.SeparatorTokenId, segmentId: 1, inputIds, attentionMask, tokenTypeIds);

        while (position < maxSequenceLength)
        {
            inputIds[row, position] = tokenizer.PaddingTokenId;
            attentionMask[row, position] = 0;
            tokenTypeIds[row, position] = 0;
            position++;
        }
    }

    private static void WriteToken(
        int row,
        ref int position,
        int tokenId,
        int segmentId,
        DenseTensor<long> inputIds,
        DenseTensor<long> attentionMask,
        DenseTensor<long> tokenTypeIds)
    {
        var column = position++;
        inputIds[row, column] = tokenId;
        attentionMask[row, column] = 1;
        tokenTypeIds[row, column] = segmentId;
    }

    private IReadOnlyDictionary<string, double> BuildScores(IReadOnlyList<RankedChunk> candidates, Tensor<float> logits, ReRankingOptions options)
    {
        var dimensions = logits.Dimensions.ToArray();
        var scores = new Dictionary<string, double>(candidates.Count, StringComparer.OrdinalIgnoreCase);

        if (dimensions.Length == 1)
        {
            for (var index = 0; index < candidates.Count && index < dimensions[0]; index++)
            {
                scores[candidates[index].Id] = Sigmoid(logits[index]);
            }

            return scores;
        }

        var labelCount = dimensions[^1];
        for (var index = 0; index < candidates.Count && index < dimensions[0]; index++)
        {
            if (labelCount <= 1)
            {
                scores[candidates[index].Id] = Sigmoid(logits[index, 0]);
                continue;
            }

            var positiveIndex = Math.Clamp(options.LocalOnnxPositiveLabelIndex, 0, labelCount - 1);
            var rowValues = new double[labelCount];
            for (var column = 0; column < labelCount; column++)
            {
                rowValues[column] = logits[index, column];
            }

            scores[candidates[index].Id] = Softmax(rowValues, positiveIndex);
        }

        return scores;
    }

    private IReadOnlyCollection<NamedOnnxValue> BuildInputs(
        DenseTensor<long> inputIds,
        DenseTensor<long> attentionMask,
        DenseTensor<long> tokenTypeIds,
        ReRankingOptions options)
    {
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(options.LocalOnnxInputIdsName, inputIds),
            NamedOnnxValue.CreateFromTensor(options.LocalOnnxAttentionMaskName, attentionMask)
        };

        if (!string.IsNullOrWhiteSpace(options.LocalOnnxTokenTypeIdsName))
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor(options.LocalOnnxTokenTypeIdsName, tokenTypeIds));
        }

        return inputs;
    }

    private LoadedArtifacts CreateLoadedArtifacts(ReRankingOptions options)
    {
        var modelPath = ResolvePath(options.LocalOnnxModelPath!);
        var vocabularyPath = ResolvePath(options.LocalOnnxVocabularyPath!);
        if (!File.Exists(modelPath) || !File.Exists(vocabularyPath))
        {
            throw new FileNotFoundException($"Local ONNX reranker asset not found. Model={modelPath}; Vocabulary={vocabularyPath}");
        }

        var session = new InferenceSession(modelPath, BuildSessionOptions());
        var tokenizer = BertTokenizer.Create(vocabularyPath, new BertOptions
        {
            LowerCaseBeforeTokenization = options.LocalOnnxLowerCase,
            ApplyBasicTokenization = true,
            SplitOnSpecialTokens = false
        });

        _scoreCache.Clear();
        _logger.LogInformation(
            "Local ONNX reranker initialized with model {ModelPath} and vocab {VocabularyPath}",
            modelPath,
            vocabularyPath);

        return new LoadedArtifacts(session, tokenizer);
    }

    private static SessionOptions BuildSessionOptions()
    {
        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
        sessionOptions.InterOpNumThreads = 1;
        sessionOptions.IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2);
        return sessionOptions;
    }

    private static string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    private static double Sigmoid(double value)
        => 1.0 / (1.0 + Math.Exp(-value));

    private static double Softmax(IReadOnlyList<double> logits, int positiveIndex)
    {
        var max = logits.Max();
        var exps = logits.Select(value => Math.Exp(value - max)).ToArray();
        var sum = exps.Sum();
        return sum == 0.0 ? 0.0 : exps[positiveIndex] / sum;
    }

    private static string BuildCacheKey(string signature, string query, IReadOnlyList<RankedChunk> candidates)
    {
        var parts = new List<string>(candidates.Count + 1)
        {
            signature,
            query.Trim()
        };

        parts.AddRange(candidates.Select(candidate => $"{candidate.Id}:{candidate.Section}:{candidate.Content}"));
        return string.Join("||", parts);
    }

    private static string BuildConfigurationSignature(ReRankingOptions options)
    {
        return string.Join("|",
            options.DedicatedProvider,
            options.LocalOnnxModelPath,
            options.LocalOnnxVocabularyPath,
            options.LocalOnnxLowerCase,
            options.LocalOnnxInputIdsName,
            options.LocalOnnxAttentionMaskName,
            options.LocalOnnxTokenTypeIdsName,
            options.LocalOnnxOutputName,
            options.LocalOnnxPositiveLabelIndex);
    }

    private sealed class LoadedArtifacts : IDisposable
    {
        public LoadedArtifacts(InferenceSession session, BertTokenizer tokenizer)
        {
            Session = session;
            Tokenizer = tokenizer;
        }

        public InferenceSession Session { get; }
        public BertTokenizer Tokenizer { get; }

        public void Dispose()
        {
            Session.Dispose();
        }
    }
}