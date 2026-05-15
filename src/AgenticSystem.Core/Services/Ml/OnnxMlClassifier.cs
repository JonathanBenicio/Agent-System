using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services.Ml;

/// <summary>
/// Classificador genérico usando ONNX Runtime.
/// Utilizado para Fast Path e validação semântica de Triggers.
/// </summary>
public class OnnxMlClassifier : IMlClassifier, IDisposable
{
    private readonly InferenceSession _session;
    private readonly ILogger<OnnxMlClassifier> _logger;

    public OnnxMlClassifier(string modelPath, ILogger<OnnxMlClassifier> logger)
    {
        var options = new SessionOptions
        {
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };
        
        _session = new InferenceSession(modelPath, options);
        _logger = logger;
        _logger.LogInformation("ONNX Classifier carregado (CPU Opt): {ModelPath}", modelPath);
    }

    public async Task<MlClassificationResult> ClassifyAsync(string input, string? modelId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new MlClassificationResult { Label = "Unknown", Confidence = 0.0f };

        return await Task.Run(() => 
        {
            try 
            {
                // De acordo com o pipeline do Trainer: 
                // Tokens -> TokenKeys -> Features (Ngrams)
                // Nota: O ONNX gerado pelo ML.NET espera os inputs originais se o pipeline for exportado corretamente.
                // O nome do input geralmente é o nome da coluna de entrada ("Text").
                
                var inputMeta = _session.InputMetadata;
                var inputName = inputMeta.Keys.First(); // Geralmente "Text"
                
                var container = new List<NamedOnnxValue>();
                var tensor = new DenseTensor<string>(new[] { input }, new[] { 1, 1 });
                container.Add(NamedOnnxValue.CreateFromTensor(inputName, tensor));

                using var results = _session.Run(container);
                
                // Mapear PredictedLabel e Score
                var predictedLabel = results.FirstOrDefault(r => r.Name == "PredictedLabel")?.AsEnumerable<string>().FirstOrDefault() ?? "Unknown";
                var scores = results.FirstOrDefault(r => r.Name == "Score")?.AsEnumerable<float>().ToArray() ?? Array.Empty<float>();
                
                var maxScore = scores.Length > 0 ? scores.Max() : 0.0f;

                return new MlClassificationResult
                {
                    Label = predictedLabel,
                    Confidence = maxScore,
                    Scores = scores.Select((s, i) => new { s, i }).ToDictionary(x => x.i.ToString(), x => x.s)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na inferência ONNX FastPath");
                return new MlClassificationResult { Label = "Error", Confidence = 0.0f };
            }
        });
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
