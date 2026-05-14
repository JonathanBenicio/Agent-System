using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models.Triage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services.Triage;

/// <summary>
/// Implementação do serviço de triagem semântica usando LLM de baixo custo (Camada 1).
/// </summary>
public class TriageService : ITriageService
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<TriageService> _logger;

    public TriageService(IChatClient chatClient, ILogger<TriageService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<QueryTriageResult> AnalyzeComplexityAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new QueryTriageResult 
            { 
                Intent = IntentType.SmallTalk, 
                Complexity = ComplexityLevel.Low 
            };
        }

        var systemPrompt = @"Você é o classificador de entrada de um sistema multi-agentes.
Sua única função é analisar a requisição do usuário e determinar sua complexidade e intenção para roteamento eficiente.

Avalie os seguintes campos:
1) Intent: 
   - SmallTalk: Saudações, cumprimentos, agradecimentos ou conversas triviais sem comandos.
   - DirectAnswer: Perguntas factuais simples que podem ser respondidas diretamente sem raciocínio complexo.
   - ComplexReasoning: Problemas que exigem raciocínio lógico, múltiplas etapas, análise de dados ou uso de ferramentas.

2) Complexity:
   - Low: Requer processamento mínimo.
   - Medium: Requer busca em memória (RAG) ou análise moderada.
   - High: Requer orquestração completa, múltiplas ferramentas ou raciocínio profundo.

3) RequiresRAG: true se precisar buscar informações na base de conhecimento ou memória do usuário.
4) RequiresTools: true se precisar executar ferramentas externas (ex: calculadora, busca web, execução de código).
5) RecommendedAgentTier: Tier sugerido (Chief, Master, Specialist, Support).

Retorne EXCLUSIVAMENTE um objeto JSON válido seguindo este esquema:
{
  ""Intent"": ""SmallTalk | DirectAnswer | ComplexReasoning"",
  ""Complexity"": ""Low | Medium | High"",
  ""RequiresRAG"": true|false,
  ""RequiresTools"": true|false,
  ""RecommendedAgentTier"": ""string""
}";

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, input)
        };

        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.Json,
            Temperature = 0.0f
        };

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, options, ct);
            var responseText = response.Text ?? string.Empty;

            var result = JsonSerializer.Deserialize<QueryTriageResult>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });

            return result ?? FallbackResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na triagem da query: {Input}", input);
            return FallbackResult();
        }
    }

    private static QueryTriageResult FallbackResult() => new()
    {
        Intent = IntentType.ComplexReasoning,
        Complexity = ComplexityLevel.High,
        RequiresRAG = true,
        RequiresTools = true,
        RecommendedAgentTier = "Chief"
    };
}
