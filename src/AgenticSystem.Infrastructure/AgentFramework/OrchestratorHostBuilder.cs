using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Construtor nativo do orquestrador hospedado, alinhado com padrão AddAIAgent.
/// Encapsula composição de agent, tools, providers e middleware em um único punto.
/// Substitui a montagem manual em OrchestratorContextFactory.
/// </summary>
public class OrchestratorHostBuilder
{
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly OrchestratorMetadata _metadata;
    private readonly IAgentFactory _agentFactory;
    private readonly OrchestratorInstructionService _instructionService;
    private readonly OrchestratorToolBindingService _toolBindingService;
    private readonly OrchestratorAuxiliaryToolService _auxiliaryToolService;
    private readonly ISkillManager _skillManager;
    private readonly RAGContextProvider? _ragContextProvider;
    private readonly IQualityGateService? _qualityGateService;
    private readonly AgentSkillsProvider? _skillsProvider;
    private readonly ILogger<OrchestratorHostBuilder> _logger;

    public OrchestratorHostBuilder(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        OrchestratorMetadata metadata,
        IAgentFactory agentFactory,
        OrchestratorInstructionService instructionService,
        OrchestratorToolBindingService toolBindingService,
        OrchestratorAuxiliaryToolService auxiliaryToolService,
        ILogger<OrchestratorHostBuilder> logger,
        ISkillManager skillManager,
        RAGContextProvider? ragContextProvider = null,
        IQualityGateService? qualityGateService = null,
        AgentSkillsProvider? skillsProvider = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _instructionService = instructionService ?? throw new ArgumentNullException(nameof(instructionService));
        _toolBindingService = toolBindingService ?? throw new ArgumentNullException(nameof(toolBindingService));
        _auxiliaryToolService = auxiliaryToolService ?? throw new ArgumentNullException(nameof(auxiliaryToolService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _skillManager = skillManager ?? throw new ArgumentNullException(nameof(skillManager));
        _ragContextProvider = ragContextProvider;
        _qualityGateService = qualityGateService;
        _skillsProvider = skillsProvider;
    }

    /// <summary>
    /// Constrói o agente orquestrador com todas as dependências, seguindo padrão nativo do MAF.
    /// </summary>
    public async Task<AIAgent> BuildAsync(
        IReadOnlyList<AgentInfo> activeAgents,
        string sessionId = "default_session",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(activeAgents);

        var auxiliaryTools = _auxiliaryToolService.GetTools();
        var toolBindings = await _toolBindingService.CreateSpecialistBindingsAsync(activeAgents, sessionId, ct);
        var allTools = new List<AITool>(toolBindings.Select(binding => binding.Tool));
        allTools.AddRange(auxiliaryTools);

        var instructions = _instructionService.GetInstructions(activeAgents, auxiliaryTools);

        // Enriquecer as instruções do orquestrador principal com as C# Skills contextuais!
        instructions = await _skillManager.BuildEnrichedPromptAsync(_metadata.Name, "orchestrator", instructions);

        _logger.LogDebug(
            "Building orchestrator agent with {SpecialistCount} specialists and {ToolCount} tools",
            toolBindings.Count,
            allTools.Count);

        return CreateHostedOrchestratorAgent(instructions, allTools);
    }

    /// <summary>
    /// [PHASE 1] Constrói um workflow de Handoff nativo para orquestração dinâmica.
    /// Permite que agentes especialistas transfiram o controle entre si autonomamente.
    /// </summary>
    public async Task<Workflow> BuildHandoffWorkflowAsync(
        IReadOnlyList<AgentInfo> activeAgents,
        string sessionId = "default_session",
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(activeAgents);

        // 1. Criar o orquestrador principal (triage agent)
        var orchestratorAgent = await BuildAsync(activeAgents, sessionId, ct);

        // 2. Resolver as instâncias reais dos agentes especialistas
        var specialistAgents = new List<AIAgent>();
        foreach (var info in activeAgents)
        {
            var agent = await _agentFactory.ResolveAgentAsync(info);
            if (agent is AIAgent frameworkAgent)
            {
                specialistAgents.Add(frameworkAgent);
            }
        }

        // 3. Configurar o grafo de handoffs usando WorkflowBuilder nativo do MAF 1.6.1
        var builder = new WorkflowBuilder(orchestratorAgent);

        foreach (var specialist in specialistAgents)
        {
            builder.BindExecutor(specialist);
            
            // Orquestrador <-> Especialista
            builder.AddEdge(orchestratorAgent, specialist, idempotent: true);
            builder.AddEdge(specialist, orchestratorAgent, idempotent: true);

            // Especialista <-> Outros Especialistas (Mesh Topology)
            foreach (var otherSpecialist in specialistAgents.Where(a => a != specialist))
            {
                builder.AddEdge(specialist, otherSpecialist, idempotent: true);
            }
        }

        _logger.LogInformation(
            "Handoff workflow built with mesh topology: 1 Orchestrator <-> {SpecialistCount} Specialists",
            specialistAgents.Count);

        return builder.Build();
    }

    /// <summary>
    /// Constrói versão síncrona (necessária para DI que ainda exige Resolve síncrono).
    /// </summary>
    public AIAgent Build(IReadOnlyList<AgentInfo> activeAgents, string sessionId = "default_session")
    {
        return BuildAsync(activeAgents, sessionId, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private AIAgent CreateHostedOrchestratorAgent(
        string instructions,
        IReadOnlyList<AITool> tools)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_metadata.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(_metadata.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentNullException.ThrowIfNull(tools);

        var chatAgent = new ChatClientAgent(
            _chatClient,
            instructions,
            _metadata.Name,
            _metadata.Description,
            tools.ToList(),
            _loggerFactory,
            _serviceProvider);

        var builder = chatAgent.AsBuilder();

        // Aplicar providers e middleware de forma declarativa
        var contextProviders = new List<MessageAIContextProvider>();
        if (_ragContextProvider is not null)
        {
            contextProviders.Add(_ragContextProvider);
        }
        if (_skillsProvider is not null)
        {
            contextProviders.Add(_skillsProvider);
        }
        if (contextProviders.Count > 0)
        {
            builder = builder.UseAIContextProviders(contextProviders.ToArray());
        }

        if (_qualityGateService is not null)
        {
            var qualityGateLogger = _loggerFactory.CreateLogger<QualityGateDelegatingAgent>();
            builder = builder.UseQualityGates(_qualityGateService, qualityGateLogger);
        }

        // Adicionar logging e telemetry nativo
        return builder
            .UseLogging(_loggerFactory)
            .UseOpenTelemetry("AgenticSystem.Orchestrator")
            .Build(_serviceProvider);
    }
}
