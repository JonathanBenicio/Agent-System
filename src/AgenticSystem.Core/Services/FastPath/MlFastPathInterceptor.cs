using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ML;
using Microsoft.Extensions.DependencyInjection;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services.FastPath
{
  public class MlFastPathInterceptor : IFastPathInterceptor
  {
    private readonly IMlClassifier? _mlClassifier;
    private const float ConfidenceThreshold = 0.80f;

    public MlFastPathInterceptor(IServiceProvider serviceProvider)
    {
      _mlClassifier = serviceProvider.GetService<IMlClassifier>();
    }

    public async Task<(bool IsFastPath, string? Response)> EvaluateAsync(string input, CancellationToken cancellationToken = default)
    {
      if (_mlClassifier == null)
        return (false, null);

      if (string.IsNullOrWhiteSpace(input))
        return (false, null);

      if (input.Length > 80) // Aumentado um pouco para suportar perguntas mais completas
        return (false, null);

      var result = await _mlClassifier.ClassifyAsync(input, ct: cancellationToken);

      if (result.Confidence < ConfidenceThreshold)
        return (false, null);

      return result.Label switch
      {
        "Greeting" => (true, "Olá! Como posso ajudar você hoje?"),
        "SmallTalk_HowAreYou" => (true, "Estou operando perfeitamente! Como posso ser útil?"),
        "SmallTalk_Thanks" => (true, "Por nada! Estou sempre à disposição."),
        "Agent_Capabilities" => (true, "Eu sou o AgenticSystem Orchestrator. Posso gerenciar suas tarefas, projetos .NET, realizar auditorias de segurança e muito mais. Como posso ajudar?"),
        "System_Status" => (true, "Todos os sistemas estão operando normalmente. Latência estável e gateways ativos."),
        "Goodbye" => (true, "Até logo! Estarei aqui quando precisar."),
        "Feedback_Positive" => (true, "Fico feliz em ajudar! Obrigado pelo feedback."),
        "Feedback_Negative" => (true, "Sinto muito por isso. Vou me esforçar para melhorar. Poderia detalhar o que houve?"),
        "User_Help" => (true, "Você pode me pedir para: criar projetos, analisar código, verificar segurança ou apenas conversar. Use comandos naturais ou /ajuda para guias."),

        _ => (false, null)
      };
    }
  }
}