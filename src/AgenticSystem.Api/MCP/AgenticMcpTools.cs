using System.ComponentModel;
using System.Security.Claims;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using ModelContextProtocol.Server;

namespace AgenticSystem.Api.MCP;

[McpServerToolType]
public sealed class AgenticMcpTools
{
    private readonly IMetaAgent _metaAgent;
    private readonly IRAGService _ragService;
    private readonly IToolManager _toolManager;
    private readonly IMCPPluginManager _pluginManager;

    public AgenticMcpTools(
        IMetaAgent metaAgent,
        IRAGService ragService,
        IToolManager toolManager,
        IMCPPluginManager pluginManager)
    {
        _metaAgent = metaAgent;
        _ragService = ragService;
        _toolManager = toolManager;
        _pluginManager = pluginManager;
    }

    [McpServerTool(Name = "list_agents", Title = "Listar Agents", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lista os agents ativos disponíveis no runtime agentic, incluindo tier, domínio e tools declaradas.")]
    public async Task<object> ListAgentsAsync(CancellationToken cancellationToken)
    {
        var agents = await _metaAgent.GetActiveAgentsAsync();

        return agents
            .OrderBy(agent => agent.Tier)
            .ThenBy(agent => agent.Name)
            .Select(agent => new
            {
                agent.Name,
                Tier = agent.Tier.ToString(),
                agent.Domain,
                agent.Description,
                agent.IsActive,
                AvailableTools = agent.AvailableTools
            })
            .ToList();
    }

    [McpServerTool(Name = "search_knowledge", Title = "Buscar Conhecimento", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Executa o pipeline RAG do AgenticSystem e retorna contexto montado, query efetiva, variantes utilizadas e chunks ranqueados.")]
    public async Task<object> SearchKnowledgeAsync(
        [Description("Consulta textual a ser enviada ao pipeline RAG.")] string query,
        [Description("Estratégia de retrieval: Default, RecentMemory, DomainKnowledge, DecisionHistory, Episodic ou Targeted.")] string strategy = "Default",
        [Description("Sessão opcional usada para retrieval episódico.")] string? sessionId = null,
        [Description("AgentId opcional para busca direcionada.")] string? agentId = null,
        [Description("Quantidade máxima de chunks após re-ranking.")] int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query is required.", nameof(query));
        }

        if (!Enum.TryParse<RetrievalStrategy>(strategy, ignoreCase: true, out var parsedStrategy))
        {
            throw new ArgumentException($"Unknown retrieval strategy '{strategy}'.", nameof(strategy));
        }

        var context = await _ragService.RetrieveContextAsync(new RAGQuery
        {
            Query = query,
            Strategy = parsedStrategy,
            SessionId = sessionId,
            AgentId = agentId,
            TopKAfterReRank = Math.Max(1, topK)
        }, cancellationToken);

        return new
        {
            context.Query,
            context.EffectiveQuery,
            context.QueryVariants,
            context.StrategyUsed,
            context.SemanticSummary,
            context.UsedSemanticCompression,
            context.OriginalContextTokens,
            context.TotalTokensUsed,
            context.CandidatesRetrieved,
            context.CandidatesAfterReRank,
            context.TotalTime,
            Context = context.BuiltContext,
            Chunks = context.Chunks.Select(chunk => new
            {
                chunk.Id,
                chunk.Source,
                chunk.Section,
                chunk.OriginalScore,
                chunk.ReRankedScore,
                chunk.Rank,
                chunk.Metadata,
                chunk.Content
            }).ToList()
        };
    }

    [McpServerTool(Name = "list_runtime_tools", Title = "Listar Tools de Runtime", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lista tools internas registradas no runtime e tools expostas por plugins MCP carregados.")]
    public async Task<object> ListRuntimeToolsAsync(CancellationToken cancellationToken)
    {
        var internalTools = await _toolManager.GetAvailableToolsAsync();
        var pluginTools = await _pluginManager.GetAllToolsAsync();

        return new
        {
            InternalTools = internalTools.Select(tool => new
            {
                tool.Id,
                tool.Name,
                tool.Description,
                Category = tool.Category.ToString(),
                tool.RequiresAuth
            }).ToList(),
            PluginTools = pluginTools.Select(tool => new
            {
                tool.PluginId,
                tool.PluginName,
                tool.ToolName,
                tool.Description
            }).ToList()
        };
    }

    [McpServerTool(Name = "execute_agent", Title = "Executar Agent", OpenWorld = true, Idempotent = false, Destructive = false)]
    [Description("Executa o MetaAgent via MCP. Se targetAgent for informado, força o roteamento para esse agent; caso contrário usa o roteamento automático.")]
    public async Task<object> ExecuteAgentAsync(
        [Description("Mensagem ou tarefa a ser processada pelo sistema agentic.")] string input,
        [Description("Nome opcional do agent alvo para execução direta.")] string? targetAgent,
        ClaimsPrincipal user,
        TenantContext tenantContext)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input is required.", nameof(input));
        }

        var userContext = new UserContext
        {
            UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.Identity?.Name
                ?? "mcp-user",
            Name = user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.Identity?.Name
                ?? "MCP User",
            TenantId = string.IsNullOrWhiteSpace(tenantContext.TenantId)
                ? Tenant.DefaultTenantId
                : tenantContext.TenantId,
            Language = "pt-BR"
        };

        var response = string.IsNullOrWhiteSpace(targetAgent)
            ? await _metaAgent.ProcessRequestAsync(input, userContext)
            : await _metaAgent.ProcessDirectRequestAsync(input, userContext, targetAgent);

        return new
        {
            response.Success,
            response.AgentName,
            AgentTier = response.AgentTier.ToString(),
            response.Content,
            response.ErrorMessage,
            response.SessionId,
            response.ActionsPerformed,
            response.ToolsUsed,
            Confidence = response.Confidence?.Value
        };
    }
}