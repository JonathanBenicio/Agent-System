using System.ComponentModel;
using Microsoft.Extensions.AI;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Fábrica estática de AITools auxiliares para o orquestrador.
/// Encapsula SmartRouter, ContextAnalyzer e RAG como tool bindings invocáveis pelo LLM.
/// Estas tools complementam os specialist tool bindings, dando ao orquestrador
/// capacidades de análise, roteamento e busca de contexto on-demand.
/// </summary>
public static class OrchestratorAuxiliaryTools
{
    public const string RetrieveContextToolName = "retrieve_context";
    public const string RouteToAgentToolName = "route_to_best_agent";
    public const string AnalyzeRequestToolName = "analyze_request";

    /// <summary>
    /// Cria AITool para busca on-demand de contexto via RAG.
    /// O LLM chama esta tool quando decide que precisa de informações adicionais.
    /// </summary>
    public static AITool CreateRetrieveContextTool(
        IRAGService ragService, IContextBudgetManager? budgetManager)
    {
        return AIFunctionFactory.Create(
            async (
                [Description("Consulta para buscar contexto relevante na base de conhecimento")]
                string query,
                CancellationToken ct) =>
            {
                var ragContext = await ragService.RetrieveContextAsync(new RAGQuery
                {
                    Query = query,
                    Scope = SearchScope.All,
                    MaxResults = 10,
                    TopKAfterReRank = 5,
                    MinRelevanceScore = 0.3
                }, ct);

                if (budgetManager != null)
                {
                    var budget = budgetManager.ResolveBudget(
                        new AnalysisResult { Complexity = ComplexityLevel.Moderate });
                    ragContext = await budgetManager.TrimContextToBudgetAsync(ragContext, budget);
                }

                return ragContext.BuiltContext ?? string.Empty;
            },
            new AIFunctionFactoryOptions
            {
                Name = RetrieveContextToolName,
                Description = "Busca contexto relevante na base de conhecimento via RAG. " +
                    "Use quando precisar de informações adicionais para responder com mais precisão."
            });
    }

    /// <summary>
    /// Cria AITool para consulta ao SmartRouter.
    /// O LLM pode consultar o roteador para decidir qual especialista é mais adequado.
    /// </summary>
    public static AITool CreateRouteToAgentTool(ISmartRouter smartRouter)
    {
        return AIFunctionFactory.Create(
            async (
                [Description("Domínio principal da solicitação (ex: personal, work, learning)")]
                string domain,
                [Description("Tipo de intenção (ex: Chat, Task, Analysis, Creative)")]
                string intent,
                CancellationToken ct) =>
            {
                var analysis = new AnalysisResult
                {
                    PrimaryDomain = domain,
                    Intent = Enum.TryParse<IntentType>(intent, true, out var parsed)
                        ? parsed : IntentType.Chat
                };

                var decision = await smartRouter.RouteAsync(analysis, new UserContext());

                return $"Agent recomendado: {decision.PrimaryAgent} " +
                       $"(confiança: {decision.ConfidenceScore:F2}). " +
                       $"Razão: {decision.RoutingReason}. " +
                       $"Fallback chain: {string.Join(" → ", decision.FallbackChain)}";
            },
            new AIFunctionFactoryOptions
            {
                Name = RouteToAgentToolName,
                Description = "Consulta o Smart Router para identificar o especialista mais adequado " +
                    "com base no domínio e intenção da solicitação."
            });
    }

    /// <summary>
    /// Cria AITool para análise de contexto/intenção.
    /// O LLM pode analisar a solicitação antes de decidir como processá-la.
    /// </summary>
    public static AITool CreateAnalyzeRequestTool(IContextAnalyzer contextAnalyzer)
    {
        return AIFunctionFactory.Create(
            async (
                [Description("Texto da solicitação do usuário para análise")]
                string input,
                CancellationToken ct) =>
            {
                var result = await contextAnalyzer.AnalyzeAsync(input, new UserContext());

                return $"Domínio: {result.PrimaryDomain}, " +
                       $"Intent: {result.Intent}, " +
                       $"Complexidade: {result.Complexity}, " +
                       $"Agent sugerido: {result.EstimatedAgent}, " +
                       $"Confiança: {result.Confidence:F2}, " +
                       $"Tier: {result.RecommendedTier}";
            },
            new AIFunctionFactoryOptions
            {
                Name = AnalyzeRequestToolName,
                Description = "Analisa uma solicitação do usuário para identificar domínio, " +
                    "intenção, complexidade e agente recomendado."
            });
    }

    /// <summary>
    /// Nomes de todas as tools auxiliares (para distinguir de tool bindings de especialistas).
    /// </summary>
    public static HashSet<string> AllToolNames =>
    [
        RetrieveContextToolName,
        RouteToAgentToolName,
        AnalyzeRequestToolName
    ];
}
