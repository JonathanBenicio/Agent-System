using System.Collections.Concurrent;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using FrameworkAgent = Microsoft.Agents.AI.AIAgent;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Constrói e cacheia o ChatClientAgent orquestrador.
/// O orquestrador recebe tool bindings dos especialistas (AsAIFunction) e
/// um system prompt dinâmico que descreve os agentes disponíveis, domínios e critérios de delegação.
/// O LLM decide qual tool/agente chamar com base no input do usuário.
/// </summary>
public class OrchestratorAgentBuilder
{
    private readonly AgentFrameworkFactory _frameworkFactory;
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<OrchestratorAgentBuilder> _logger;

    // Middleware de domínio (Reflection + QualityGates)
    private readonly IReflectionEngine? _reflectionEngine;
    private readonly IQualityGateService? _qualityGateService;

    // RAG context provider — injeção automática de contexto antes de cada LLM call
    private readonly RAGContextProvider? _ragContextProvider;

    // Tools auxiliares — on-demand via tool calling (RAG, SmartRouter, ContextAnalyzer)
    private readonly List<AITool> _auxiliaryTools = [];

    // Cache invalidado quando a lista de agentes muda (por hash dos nomes)
    private readonly ConcurrentDictionary<string, CachedOrchestrator> _cache = new();

    public OrchestratorAgentBuilder(
        AgentFrameworkFactory frameworkFactory,
        IAgentFactory agentFactory,
        ILogger<OrchestratorAgentBuilder> logger,
        IRAGService? ragService = null,
        IContextBudgetManager? contextBudgetManager = null,
        ISmartRouter? smartRouter = null,
        IContextAnalyzer? contextAnalyzer = null,
        IReflectionEngine? reflectionEngine = null,
        IQualityGateService? qualityGateService = null)
    {
        _frameworkFactory = frameworkFactory ?? throw new ArgumentNullException(nameof(frameworkFactory));
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reflectionEngine = reflectionEngine;
        _qualityGateService = qualityGateService;

        // RAG Context Provider — injeção automática de contexto via MessageAIContextProvider
        if (ragService != null)
        {
            _ragContextProvider = new RAGContextProvider(
                ragService,
                contextBudgetManager,
                _frameworkFactory.LoggerFactory.CreateLogger<RAGContextProvider>());

            _auxiliaryTools.Add(
                OrchestratorAuxiliaryTools.CreateRetrieveContextTool(ragService, contextBudgetManager));
        }

        // SmartRouter como tool — o LLM consulta para sugestão de roteamento
        if (smartRouter != null)
        {
            _auxiliaryTools.Add(
                OrchestratorAuxiliaryTools.CreateRouteToAgentTool(smartRouter));
        }

        // ContextAnalyzer como tool — o LLM analisa domínio/intenção on-demand
        if (contextAnalyzer != null)
        {
            _auxiliaryTools.Add(
                OrchestratorAuxiliaryTools.CreateAnalyzeRequestTool(contextAnalyzer));
        }

        if (_auxiliaryTools.Count > 0)
        {
            _logger.LogInformation(
                "Orchestrator auxiliary tools registered: {Tools}",
                string.Join(", ", _auxiliaryTools.Select(t => t.Name)));
        }
    }

    /// <summary>
    /// Obtém ou cria o ChatClientAgent orquestrador com tool bindings dos especialistas.
    /// Cada sessionId mantém seus próprios tool bindings (porque sessions são agent-specific).
    /// </summary>
    public async Task<OrchestratorContext> GetOrCreateOrchestratorAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        var agents = (await _agentFactory.GetAllAgentsAsync()).ToList();
        var cacheKey = BuildCacheKey(agents);

        // Tool bindings são session-specific, então sempre recria para nova sessão
        var toolBindings = await CreateToolBindingsAsync(agents, sessionId, ct);
        var tools = toolBindings.Select(tb => tb.Tool).ToList();

        // Merge auxiliary tools (RAG, SmartRouter, ContextAnalyzer) com specialist tools
        var allTools = new List<AITool>(tools);
        allTools.AddRange(_auxiliaryTools);

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogDebug(
                "Orchestrator agent reused from cache (key={CacheKey}, tools={ToolCount}, auxiliary={AuxCount})",
                cacheKey, tools.Count, _auxiliaryTools.Count);

            // Recria o orchestrator com novos tool bindings (session-specific)
            var refreshedAgent = await CreateOrchestratorAgentAsync(cached.Instructions, allTools, ct);
            return new OrchestratorContext(refreshedAgent, toolBindings);
        }

        var instructions = BuildOrchestratorInstructions(agents, _auxiliaryTools);

        _cache[cacheKey] = new CachedOrchestrator(instructions);
        _logger.LogInformation(
            "Orchestrator agent built with {AgentCount} specialist tools + {AuxCount} auxiliary tools",
            agents.Count, _auxiliaryTools.Count);

        var orchestratorAgent = await CreateOrchestratorAgentAsync(instructions, allTools, ct);
        return new OrchestratorContext(orchestratorAgent, toolBindings);
    }

    /// <summary>
    /// Invalida o cache (chamado quando pool de agentes muda — criação/remoção dinâmica).
    /// </summary>
    public void InvalidateCache()
    {
        _cache.Clear();
        _logger.LogInformation("Orchestrator cache invalidated");
    }

    private async Task<FrameworkAgent> CreateOrchestratorAgentAsync(
        string instructions,
        IList<AITool> specialistTools,
        CancellationToken ct)
    {
        var chatAgent = new ChatClientAgent(
            _frameworkFactory.ChatClient,
            instructions,
            "Orchestrator",
            "Agente orquestrador que coordena especialistas via tool calling",
            specialistTools,
            _frameworkFactory.LoggerFactory,
            _frameworkFactory.ServiceProvider);

        var builder = chatAgent.AsBuilder();

        // Injetar RAG context provider se disponível (MessageAIContextProvider)
        if (_ragContextProvider != null)
        {
            builder = builder.UseAIContextProviders(_ragContextProvider);
        }

        // Middleware de domínio — QualityGates (pré/pós execução)
        if (_qualityGateService != null)
        {
            builder = builder.UseQualityGates(
                _qualityGateService,
                _frameworkFactory.LoggerFactory.CreateLogger<QualityGateDelegatingAgent>());
        }

        // Middleware de domínio — Reflection (pós-ação)
        if (_reflectionEngine != null)
        {
            builder = builder.UseReflection(
                _reflectionEngine,
                _frameworkFactory.LoggerFactory.CreateLogger<ReflectionDelegatingAgent>());
        }

        return builder
            .UseLogging(_frameworkFactory.LoggerFactory)
            .UseOpenTelemetry("AgenticSystem.Orchestrator")
            .Build(_frameworkFactory.ServiceProvider);
    }

    private async Task<List<AgentToolBinding>> CreateToolBindingsAsync(
        List<AgentInfo> agentInfos,
        string sessionId,
        CancellationToken ct)
    {
        var bindings = new List<AgentToolBinding>();

        foreach (var info in agentInfos.Where(a => a.IsActive))
        {
            try
            {
                // Resolve o IAgent real via factory
                var analysis = new AnalysisResult
                {
                    PrimaryDomain = info.Domain,
                    EstimatedAgent = info.Name,
                    RecommendedTier = info.Tier,
                    Confidence = 1.0,
                    Intent = IntentType.Chat,
                    RequiredTools = info.AvailableTools
                };

                var agent = await _agentFactory.GetOrCreateAgentAsync(analysis);

                // Desembrulha o adapter se necessário — precisamos do IAgent puro
                var rawAgent = agent is AgentFrameworkAdapter adapter
                    ? GetInnerAgent(adapter)
                    : agent;

                var binding = await _frameworkFactory.CreateToolBindingAsync(rawAgent, sessionId, ct);
                if (binding is not null)
                {
                    bindings.Add(binding);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to create tool binding for agent {Agent}, skipping", info.Name);
            }
        }

        return bindings;
    }

    private static string BuildOrchestratorInstructions(
        List<AgentInfo> agents, IReadOnlyList<AITool> auxiliaryTools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Você é o orquestrador central do sistema Baianinho-Labs.");
        sb.AppendLine("Sua responsabilidade é analisar a solicitação do usuário e delegar para o especialista mais adequado.");
        sb.AppendLine();
        sb.AppendLine("## Regras de Delegação");
        sb.AppendLine("1. Analise o domínio, intent e complexidade da solicitação.");
        sb.AppendLine("2. Chame o tool do especialista mais adequado passando o input original do usuário.");
        sb.AppendLine("3. Se a solicitação envolver múltiplos domínios, chame múltiplos especialistas e consolide as respostas.");
        sb.AppendLine("4. Se nenhum especialista for adequado, responda diretamente com uma mensagem informativa.");
        sb.AppendLine("5. Retorne a resposta do especialista ao usuário, sem adicionar comentários desnecessários.");
        sb.AppendLine("6. Sempre responda no mesmo idioma do usuário.");
        sb.AppendLine();

        if (auxiliaryTools.Count > 0)
        {
            sb.AppendLine("## Ferramentas Auxiliares");
            sb.AppendLine("Você tem acesso a ferramentas auxiliares que ajudam na tomada de decisão:");
            foreach (var tool in auxiliaryTools)
            {
                var description = (tool as AIFunction)?.Description ?? tool.Name;
                sb.AppendLine($"- **{tool.Name}**: {description}");
            }
            sb.AppendLine();
            sb.AppendLine("Use `retrieve_context` quando precisar de informações da base de conhecimento.");
            sb.AppendLine("Use `analyze_request` ou `route_to_best_agent` se tiver dúvida sobre qual especialista delegar.");
            sb.AppendLine();
        }

        sb.AppendLine("## Especialistas Disponíveis");

        foreach (var agent in agents.Where(a => a.IsActive))
        {
            sb.AppendLine();
            sb.AppendLine($"### {agent.Name}");
            sb.AppendLine($"- **Domínio:** {agent.Domain}");
            sb.AppendLine($"- **Tier:** {agent.Tier}");
            sb.AppendLine($"- **Descrição:** {agent.Description}");
            if (agent.AvailableTools.Count > 0)
            {
                sb.AppendLine($"- **Tools:** {string.Join(", ", agent.AvailableTools)}");
            }
        }

        return sb.ToString();
    }

    private static string BuildCacheKey(List<AgentInfo> agents)
    {
        var names = agents
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => a.Name);
        return string.Join("|", names);
    }

    private static IAgent GetInnerAgent(AgentFrameworkAdapter adapter)
    {
        // Usa reflection para acessar o _innerAgent privado do adapter
        var field = typeof(AgentFrameworkAdapter).GetField("_innerAgent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(adapter) as IAgent ?? (IAgent)adapter;
    }

    private sealed record CachedOrchestrator(string Instructions);
}

/// <summary>
/// Contexto do orquestrador montado: o agent do framework + os bindings dos especialistas.
/// </summary>
public sealed record OrchestratorContext(
    FrameworkAgent OrchestratorAgent,
    List<AgentToolBinding> SpecialistBindings);
