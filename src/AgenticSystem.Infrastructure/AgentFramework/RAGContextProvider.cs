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
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;

    internal const string ContextMarker = "[Contexto Relevante da Base de Conhecimento]";

    public RAGContextProvider(
        IRAGService ragService,
        IContextBudgetManager? budgetManager,
        ILogger<RAGContextProvider> logger,
        IServiceProvider serviceProvider,
        ITenantContextAccessor tenantContextAccessor,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor)
    {
        _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
        _budgetManager = budgetManager;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        _llmRuntimeContextAccessor = llmRuntimeContextAccessor ?? throw new ArgumentNullException(nameof(llmRuntimeContextAccessor));
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
        var runtimeContext = _llmRuntimeContextAccessor.Current;
        var tenantId = runtimeContext?.TenantId 
            ?? _tenantContextAccessor.Current?.TenantId 
            ?? throw new InvalidOperationException("Zero Trust: Tenant ID must be resolved for RAG context extraction.");
        var userId = runtimeContext?.UserId;

        var specifiedRoomId = runtimeContext?.KnowledgeRoomId;
        var sessionId = runtimeContext?.SessionId;

        List<string> allowedRoomIds = [];
        Dictionary<string, string> filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(specifiedRoomId))
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Zero Trust: KnowledgeRoomId '{RoomId}' is specified but UserId is missing from context. RAG search bypassed.", specifiedRoomId);
                return [];
            }

            using var scope = _serviceProvider.CreateScope();
            var roomService = scope.ServiceProvider.GetRequiredService<IKnowledgeRoomService>();
            var room = await roomService.GetRoomAsync(specifiedRoomId, tenantId, userId, ct);
            if (room is null)
            {
                _logger.LogWarning("Zero Trust Check Failed: User '{UserId}' does not have access to KnowledgeRoom '{RoomId}' in Tenant '{TenantId}'. RAG search bypassed.", userId, specifiedRoomId, tenantId);
                return [];
            }

            allowedRoomIds = [specifiedRoomId];
            filters["room_ids"] = specifiedRoomId;
        }
        else if (!string.IsNullOrWhiteSpace(sessionId))
        {
            // Chat livre isolado por sessão
            filters["collection"] = sessionId;
        }
        else
        {
            // Comportamento padrão: Salas vinculadas ao agente especialista
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

            filters["room_ids"] = string.Join(",", allowedRoomIds);
        }

        try
        {
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
