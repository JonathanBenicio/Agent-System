using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly OrchestratorInstructionService _instructionService;
    private readonly OrchestratorToolBindingService _toolBindingService;
    private readonly OrchestratorAuxiliaryToolService _auxiliaryToolService;
    private readonly ISkillManager _skillManager;
    private readonly RAGContextProvider? _ragContextProvider;
    private readonly IQualityGateService? _qualityGateService;
    private readonly AgentSkillsProvider? _skillsProvider;
    private readonly ITenantContextAccessor? _tenantContextAccessor;
    private readonly ILogger<OrchestratorHostBuilder> _logger;

    public OrchestratorHostBuilder(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        OrchestratorMetadata metadata,
        OrchestratorInstructionService instructionService,
        OrchestratorToolBindingService toolBindingService,
        OrchestratorAuxiliaryToolService auxiliaryToolService,
        ILogger<OrchestratorHostBuilder> logger,
        ISkillManager skillManager,
        RAGContextProvider? ragContextProvider = null,
        IQualityGateService? qualityGateService = null,
        AgentSkillsProvider? skillsProvider = null,
        ITenantContextAccessor? tenantContextAccessor = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _instructionService = instructionService ?? throw new ArgumentNullException(nameof(instructionService));
        _toolBindingService = toolBindingService ?? throw new ArgumentNullException(nameof(toolBindingService));
        _auxiliaryToolService = auxiliaryToolService ?? throw new ArgumentNullException(nameof(auxiliaryToolService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _skillManager = skillManager ?? throw new ArgumentNullException(nameof(skillManager));
        _ragContextProvider = ragContextProvider;
        _qualityGateService = qualityGateService;
        _skillsProvider = skillsProvider;
        _tenantContextAccessor = tenantContextAccessor;
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
        var boundAgentNames = toolBindings
            .Select(binding => binding.Agent.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableSpecialists = activeAgents
            .Where(agent => agent.IsActive && boundAgentNames.Contains(agent.Name))
            .ToList();
        var allTools = new List<AITool>(toolBindings.Select(binding => binding.Tool));
        allTools.AddRange(auxiliaryTools);

        // Resolve context state to store resolved bindings
        var state = _serviceProvider.GetService<OrchestratorContextState>();
        if (state != null)
        {
            state.SpecialistBindings = toolBindings;
        }

        var instructions = _instructionService.GetInstructions(availableSpecialists, auxiliaryTools);

        // Enriquecer as instruções do orquestrador principal com as C# Skills contextuais!
        instructions = await _skillManager.BuildEnrichedPromptAsync(_metadata.Name, "orchestrator", instructions);

        _logger.LogDebug(
            "Building orchestrator agent with {SpecialistCount} specialists and {ToolCount} tools",
            toolBindings.Count,
            allTools.Count);

        return CreateHostedOrchestratorAgent(instructions, allTools);
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

        if (_tenantContextAccessor is not null)
        {
            var fidesLogger = _loggerFactory.CreateLogger<AgenticSystem.Infrastructure.Security.FidesDataProtectionMiddleware>();
            builder = builder.UseFidesDataProtection(_tenantContextAccessor, fidesLogger);
        }

        // Adicionar logging e telemetry nativo
        return builder
            .UseLogging(_loggerFactory)
            .UseOpenTelemetry("AgenticSystem.Orchestrator")
            .Build(_serviceProvider);
    }
}
