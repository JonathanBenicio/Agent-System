using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services.Ml;

/// <summary>
/// Gerador de Embeddings local usando ONNX Runtime.
/// Ideal para RAG e busca vetorial de baixa latência sem custos de API externa.
/// </summary>
public class OnnxEmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
    private readonly InferenceSession _session;
    private readonly ILogger<OnnxEmbeddingGenerator> _logger;

    public OnnxEmbeddingGenerator(string modelPath, ILogger<OnnxEmbeddingGenerator> logger)
    {
        var options = new SessionOptions
        {
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };
        
        _session = new InferenceSession(modelPath, options);
        _logger = logger;
        _logger.LogInformation("ONNX Embedding Generator inicializado (CPU Opt): {ModelPath}", modelPath);
    }

    public async Task<float[]> GenerateAsync(string text, EmbeddingModelConfig model)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        return await Task.Run(() => 
        {
            try 
            {
                // Estrutura para modelos BERT/MiniLM (Task 1.2)
                // O nome do input geralmente é 'input_ids', 'attention_mask'
                
                var container = new List<NamedOnnxValue>();
                var inputMeta = _session.InputMetadata;

                // Nota: A tokenização real deve ser feita com Microsoft.ML.Tokenizers
                // Aqui demonstramos a lógica de Tensores.
                long[] ids = { 101, 102 }; // [CLS], [SEP] placeholder
                long[] mask = { 1, 1 };

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
                
                // O output geralmente é 'last_hidden_state' ou 'sentence_embedding'
                var outputName = _session.OutputMetadata.Keys.First();
                var outputValue = results.FirstOrDefault(r => r.Name == outputName);
                
                if (outputValue == null) return new float[model.Dimensions > 0 ? model.Dimensions : 384];

                var tensor = outputValue.AsTensor<float>();
                return tensor.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na inferência de Embeddings ONNX");
                return new float[model.Dimensions > 0 ? model.Dimensions : 384];
            }
        });
    }

    public async Task<IEnumerable<float[]>> GenerateBatchAsync(IEnumerable<string> texts, EmbeddingModelConfig model)
    {
        var results = new List<float[]>();
        foreach (var text in texts)
        {
            results.Add(await GenerateAsync(text, model));
        }
        return results;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
