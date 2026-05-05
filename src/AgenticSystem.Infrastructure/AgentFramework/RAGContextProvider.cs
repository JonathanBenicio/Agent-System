using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// MessageAIContextProvider que injeta contexto RAG automaticamente antes de cada execução do agente.
/// O contexto é buscado via IRAGService com base na última mensagem do usuário
/// e trimado via IContextBudgetManager quando disponível.
/// Evita re-injeção se contexto RAG já foi adicionado na conversação corrente.
/// </summary>
public class RAGContextProvider : MessageAIContextProvider
{
    private readonly IRAGService _ragService;
    private readonly IContextBudgetManager? _budgetManager;
    private readonly ILogger<RAGContextProvider> _logger;

    internal const string ContextMarker = "[Contexto Relevante da Base de Conhecimento]";

    public RAGContextProvider(
        IRAGService ragService,
        IContextBudgetManager? budgetManager,
        ILogger<RAGContextProvider> logger)
    {
        _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
        _budgetManager = budgetManager;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context, CancellationToken ct)
    {
        // Evitar re-injeção se RAG context já foi adicionado nesta conversação
        if (context.RequestMessages.Any(m =>
            m.Role == ChatRole.System &&
            m.Text?.Contains(ContextMarker, StringComparison.Ordinal) == true))
        {
            return [];
        }

        var lastUserMsg = context.RequestMessages.LastOrDefault(m => m.Role == ChatRole.User);
        if (lastUserMsg is null) return [];

        var query = lastUserMsg.Text;
        if (string.IsNullOrWhiteSpace(query)) return [];

        try
        {
            var ragContext = await _ragService.RetrieveContextAsync(new RAGQuery
            {
                Query = query,
                Scope = SearchScope.All,
                MaxResults = 10,
                TopKAfterReRank = 5,
                MinRelevanceScore = 0.3
            }, ct);

            if (_budgetManager != null)
            {
                var budget = _budgetManager.ResolveBudget(
                    new AnalysisResult { Complexity = ComplexityLevel.Moderate });
                ragContext = await _budgetManager.TrimContextToBudgetAsync(ragContext, budget);
            }

            if (string.IsNullOrWhiteSpace(ragContext.BuiltContext)) return [];

            _logger.LogDebug(
                "RAG context injected via provider: {Tokens} tokens, {Chunks} chunks",
                ragContext.TotalTokensUsed, ragContext.CandidatesAfterReRank);

            return [new ChatMessage(ChatRole.System, $"{ContextMarker}\n{ragContext.BuiltContext}")];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG context retrieval failed in provider, proceeding without context");
            return [];
        }
    }
}
