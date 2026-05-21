using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// MessageAIContextProvider que injeta contexto RAG automaticamente antes de cada execução do agente.
/// O contexto é buscado via IRAGService com base na última mensagem do usuário,
/// restringindo a busca vetorial às Salas de Conhecimento associadas ao agente ativo (Zero Trust).
/// </summary>
public class RAGContextProvider : MessageAIContextProvider
{
    private readonly IRAGService _ragService;
    private readonly IContextBudgetManager? _budgetManager;
    private readonly ILogger<RAGContextProvider> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    internal const string ContextMarker = "[Contexto Relevante da Base de Conhecimento]";

    public RAGContextProvider(
        IRAGService ragService,
        IContextBudgetManager? budgetManager,
        ILogger<RAGContextProvider> logger,
        IServiceProvider serviceProvider,
        ITenantContextAccessor tenantContextAccessor)
    {
        _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
        _budgetManager = budgetManager;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
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

        var agentName = context.Agent?.Name;
        var tenantId = _tenantContextAccessor.Current?.TenantId ?? Tenant.DefaultTenantId;

        // Buscar as salas vinculadas a este especialista
        List<string> allowedRoomIds = [];
        if (!string.IsNullOrWhiteSpace(agentName))
        {
            using var scope = _serviceProvider.CreateScope();
            var agentRoomStore = scope.ServiceProvider.GetRequiredService<IAgentKnowledgeRoomStore>();
            var roomIds = await agentRoomStore.GetRoomIdsForAgentAsync(agentName, tenantId, ct);
            allowedRoomIds = roomIds.ToList();
        }

        // Política de Zero Trust: Se o agente não possui associação a nenhuma sala de conhecimento no tenant,
        // o RAG é expressamente impedido de pesquisar e retorna vazio.
        if (allowedRoomIds.Count == 0)
        {
            _logger.LogWarning("Zero Trust: Agent '{AgentName}' does not have any assigned knowledge rooms. RAG search bypassed.", agentName ?? "Unknown");
            return [];
        }

        try
        {
            var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "room_ids", string.Join(",", allowedRoomIds) }
            };

            var ragContext = await _ragService.RetrieveContextAsync(new RAGQuery
            {
                Query = query,
                Scope = SearchScope.All,
                MaxResults = 10,
                TopKAfterReRank = 5,
                MinRelevanceScore = 0.3,
                Filters = filters
            }, ct);

            if (_budgetManager != null)
            {
                var budget = _budgetManager.ResolveBudget(
                    new AnalysisResult { Complexity = ComplexityLevel.Moderate });
                ragContext = await _budgetManager.TrimContextToBudgetAsync(ragContext, budget);
            }

            if (string.IsNullOrWhiteSpace(ragContext.BuiltContext)) return [];

            _logger.LogDebug(
                "RAG context injected via provider for agent '{AgentName}': {Tokens} tokens, {Chunks} chunks from {RoomCount} room(s)",
                agentName, ragContext.TotalTokensUsed, ragContext.CandidatesAfterReRank, allowedRoomIds.Count);

            return [new ChatMessage(ChatRole.System, $"{ContextMarker}\n{ragContext.BuiltContext}")];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG context retrieval failed in provider, proceeding without context");
            return [];
        }
    }
}
