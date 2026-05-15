using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services.Ml;

/// <summary>
/// Implementação de Re-ranking usando ONNX Runtime.
/// Utiliza um modelo Cross-Encoder para calcular a relevância entre query e documentos.
/// </summary>
public class OnnxReRanker : IReRanker, IDisposable
{
    private readonly InferenceSession _session;
    private readonly ILogger<OnnxReRanker> _logger;

    public OnnxReRanker(string modelPath, ILogger<OnnxReRanker> logger)
    {
        var options = new SessionOptions
        {
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };
        
        _session = new InferenceSession(modelPath, options);
        _logger = logger;
        _logger.LogInformation("ONNX ReRanker inicializado (CPU Opt): {ModelPath}", modelPath);
    }

    public async Task<IReadOnlyList<RankedChunk>> ReRankAsync(
        string query, 
        IReadOnlyList<SearchMatch> candidates, 
        int topK = 5, 
        CancellationToken ct = default)
    {
        if (candidates == null || candidates.Count == 0)
            return Array.Empty<RankedChunk>();

        var results = new List<RankedChunk>();

        foreach (var candidate in candidates)
        {
            // Nota: Em uma implementação real, usaríamos um Tokenizer (ex: BertTokenizer) 
            // para converter o par (query, document) em tensores.
            // Aqui demonstramos a lógica de inferência ONNX.
            
            float score = await InferScoreAsync(query, candidate.Content, ct);
            
            results.Add(new RankedChunk
            {
                Id = candidate.Id,
                Content = candidate.Content,
                OriginalScore = candidate.Score,
                ReRankedScore = score,
                Metadata = candidate.Metadata
            });
        }

        return results
            .OrderByDescending(x => x.ReRankedScore)
            .Take(topK)
            .ToList();
    }

    private async Task<float> InferScoreAsync(string query, string document, CancellationToken ct)
    {
        return await Task.Run(() => 
        {
            try 
            {
                // Cross-Encoder (Task 1.2 B)
                // O modelo ONNX espera pares (query, document) tokenizados.
                // Geralmente inputs: 'input_ids', 'attention_mask'
                
                var container = new List<NamedOnnxValue>();
                var inputMeta = _session.InputMetadata;

                // Nota: A tokenização real do par (query, document) 
                // deve ser feita com Microsoft.ML.Tokenizers
                long[] ids = { 101, 102, 103 }; // [CLS] ... [SEP] ... [SEP] placeholder
                long[] mask = { 1, 1, 1 };

                if (inputMeta.ContainsKey("input_ids"))
                {
                    var tensorIds = new DenseTensor<long>(ids, new[] { 1, ids.Length });
                    container.Add(NamedOnnxValue.CreateFromTensor("input_ids", tensorIds));
                }
                
                if (inputMeta.ContainsKey("attention_mask"))
                {
                    var tensorMask = new DenseTensor<long>(mask, new[] { 1, mask.Length });
                    container.Add(NamedOnnxValue.CreateFromTensor("attention_mask", tensorMask));
                }

                using var results = _session.Run(container);
                
                // O output de um Cross-Encoder é um único logit/score
                var outputName = _session.OutputMetadata.Keys.First();
                var score = results.FirstOrDefault(r => r.Name == outputName)?.AsTensor<float>().ToArray().FirstOrDefault() ?? 0.0f;

                return score;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na inferência do ReRanker ONNX");
                return 0.0f;
            }
        });
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
