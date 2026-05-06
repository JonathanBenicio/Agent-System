using AgenticSystem.Core.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Compõe o OrchestratorContext final para uma sessão específica.
/// </summary>
public class OrchestratorContextFactory
{
    private readonly IAgentFactory _agentFactory;
    private readonly ILLMRuntimeContextAccessor _runtimeContextAccessor;
    private readonly OrchestratorMetadata _metadata;
    private readonly OrchestratorAuxiliaryToolService _auxiliaryToolService;
    private readonly OrchestratorInstructionService _instructionService;
    private readonly OrchestratorToolBindingService _toolBindingService;
    private readonly AgentFrameworkFactory _frameworkFactory;
    private readonly RAGContextProvider? _ragContextProvider;
    private readonly IQualityGateService? _qualityGateService;
    private readonly ILogger<QualityGateDelegatingAgent>? _qualityGateLogger;
    private readonly ILogger<OrchestratorContextFactory> _logger;

    public OrchestratorContextFactory(
        IAgentFactory agentFactory,
        ILLMRuntimeContextAccessor runtimeContextAccessor,
        OrchestratorMetadata metadata,
        OrchestratorAuxiliaryToolService auxiliaryToolService,
        OrchestratorInstructionService instructionService,
        OrchestratorToolBindingService toolBindingService,
        AgentFrameworkFactory frameworkFactory,
        ILogger<OrchestratorContextFactory> logger,
        IRAGService? ragService = null,
        IContextBudgetManager? contextBudgetManager = null,
        IQualityGateService? qualityGateService = null)
    {
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _runtimeContextAccessor = runtimeContextAccessor ?? throw new ArgumentNullException(nameof(runtimeContextAccessor));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _auxiliaryToolService = auxiliaryToolService ?? throw new ArgumentNullException(nameof(auxiliaryToolService));
        _instructionService = instructionService ?? throw new ArgumentNullException(nameof(instructionService));
        _toolBindingService = toolBindingService ?? throw new ArgumentNullException(nameof(toolBindingService));
        _frameworkFactory = frameworkFactory ?? throw new ArgumentNullException(nameof(frameworkFactory));
        _qualityGateService = qualityGateService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (ragService is not null)
        {
            _ragContextProvider = new RAGContextProvider(
                ragService,
                contextBudgetManager,
                _frameworkFactory.LoggerFactory.CreateLogger<RAGContextProvider>());
        }

        if (qualityGateService is not null)
        {
            _qualityGateLogger = _frameworkFactory.LoggerFactory.CreateLogger<QualityGateDelegatingAgent>();
        }
    }

    public OrchestratorContext Resolve()
    {
        // O hosting nativo ainda resolve dependências de forma síncrona.
        return ResolveAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    public async Task<OrchestratorContext> ResolveAsync(CancellationToken ct = default)
    {
        var sessionId = ResolveRequiredSessionId();
        var activeAgents = await GetActiveAgentsAsync();
        return await CreateAsync(activeAgents, sessionId, ct);
    }

    public async Task<OrchestratorContext> CreateAsync(
        IReadOnlyList<AgentInfo> activeAgents,
        string sessionId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(activeAgents);

        var auxiliaryTools = _auxiliaryToolService.GetTools();

        // Tool bindings são session-specific, então sempre recria para nova sessão.
        var toolBindings = await _toolBindingService.CreateSpecialistBindingsAsync(activeAgents, sessionId, ct);
        var allTools = new List<AITool>(toolBindings.Select(binding => binding.Tool));
        allTools.AddRange(auxiliaryTools);

        var instructions = _instructionService.GetInstructions(activeAgents, auxiliaryTools);
        var orchestratorAgent = CreateHostedOrchestratorAgent(instructions, allTools);

        _logger.LogDebug(
            "Orchestrator context composed for session {SessionId} with {SpecialistCount} specialist bindings and {AuxiliaryCount} auxiliary tools",
            sessionId,
            toolBindings.Count,
            auxiliaryTools.Count);

        return new OrchestratorContext(orchestratorAgent, toolBindings);
    }

    public void Invalidate()
    {
        _instructionService.Invalidate();
        _logger.LogInformation("Orchestrator context factory cache invalidated");
    }

    private async Task<List<AgentInfo>> GetActiveAgentsAsync()
    {
        return (await _agentFactory.GetAllAgentsAsync())
            .Where(agent => agent.IsActive)
            .ToList();
    }

    private string ResolveRequiredSessionId()
    {
        var sessionId = _runtimeContextAccessor.Current?.SessionId;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException(
                "Hosted orchestrator resolution requires an active runtime context with a session id.");
        }

        return sessionId;
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
            _frameworkFactory.ChatClient,
            instructions,
            _metadata.Name,
            _metadata.Description,
            tools.ToList(),
            _frameworkFactory.LoggerFactory,
            _frameworkFactory.ServiceProvider);

        var builder = chatAgent.AsBuilder();

        if (_ragContextProvider is not null)
        {
            builder = builder.UseAIContextProviders(_ragContextProvider);
        }

        if (_qualityGateService is not null)
        {
            builder = builder.UseQualityGates(
                _qualityGateService,
                _qualityGateLogger!);
        }

        return builder
            .UseLogging(_frameworkFactory.LoggerFactory)
            .UseOpenTelemetry("AgenticSystem.Orchestrator")
            .Build(_frameworkFactory.ServiceProvider);
    }
}