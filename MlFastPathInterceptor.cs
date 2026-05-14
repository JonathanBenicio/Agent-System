using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ML;

namespace AgenticSystem.Core.Services.FastPath
{
  public class MlFastPathInterceptor : IFastPathInterceptor
  {
    private readonly PredictionEnginePool<FastPathModelInput, FastPathModelOutput> _predictionEnginePool;
    private const float ConfidenceThreshold = 0.80f; // Ajuste conforme a calibração do seu modelo

    public MlFastPathInterceptor(PredictionEnginePool<FastPathModelInput, FastPathModelOutput> predictionEnginePool)
    {
      _predictionEnginePool = predictionEnginePool;
    }

    public Task<(bool IsFastPath, string? Response)> EvaluateAsync(string input, CancellationToken cancellationToken = default)
    {
      if (string.IsNullOrWhiteSpace(input))
        return Task.FromResult((false, (string?)null));

      // Mantemos a heurística de tamanho para evitar passar textos longos desnecessários pelo modelo ML
      if (input.Length > 60)
        return Task.FromResult((false, (string?)null));

      // Executa a classificação usando o pool otimizado
      var prediction = _predictionEnginePool.Predict(new FastPathModelInput { Text = input });

      // Verifica a confiança do modelo (Score máximo entre as classes previstas)
      var maxScore = prediction.Score != null && prediction.Score.Length > 0 ? prediction.Score.Max() : 1.0f;

      if (maxScore < ConfidenceThreshold)
        return Task.FromResult((false, (string?)null));

      // Toma decisão instantânea baseada na intenção detectada ("PredictedLabel")
      return prediction.Intent switch
      {
        "Greeting" => Task.FromResult((true, "Olá! Como posso ajudar você hoje?")),
        "SmallTalk_HowAreYou" => Task.FromResult((true, "Estou operando perfeitamente! Como posso ser útil?")),
        "SmallTalk_Thanks" => Task.FromResult((true, "Por nada! Estou sempre à disposição.")),

        _ => Task.FromResult((false, (string?)null)) // Unknown ou intenção de negócio real vai pro LLM
      };
    }
  }
}