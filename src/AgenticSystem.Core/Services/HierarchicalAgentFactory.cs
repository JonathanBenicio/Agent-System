using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Agents;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

public class HierarchicalAgentFactory : IAgentFactory
{
    private readonly ConcurrentDictionary<string, IAgent> _agentPool = new();
    private readonly IChatClient _chatClient;
    private readonly ISkillManager _skillManager;
    private readonly IAgentMemoryService? _agentMemoryService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HierarchicalAgentFactory> _logger;

    public HierarchicalAgentFactory(
        IChatClient chatClient,
        ISkillManager skillManager,
        ILoggerFactory loggerFactory,
        ILogger<HierarchicalAgentFactory> logger,
        IAgentMemoryService? agentMemoryService = null)
    {
        _chatClient = chatClient;
        _skillManager = skillManager;
        _agentMemoryService = agentMemoryService;
        _loggerFactory = loggerFactory;
        _logger = logger;
        InitializeDefaultAgents();
    }

    public Task<IAgent> ResolveAgentAsync(AnalysisResult context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ResolveAgentByIdentityAsync(context.EstimatedAgent, context.PrimaryDomain);
    }

    public Task<IAgent> ResolveAgentAsync(AgentInfo agentInfo)
    {
        ArgumentNullException.ThrowIfNull(agentInfo);
        return ResolveAgentByIdentityAsync(agentInfo.Name, agentInfo.Domain);
    }

    private Task<IAgent> ResolveAgentByIdentityAsync(string? requestedAgentName, string? fallbackDomain)
    {
        var agentName = ResolveAgentPoolKey(requestedAgentName, fallbackDomain);

        if (_agentPool.TryGetValue(agentName, out var existingAgent) && existingAgent.IsActive)
        {
            _logger.LogDebug("♻️ Reusing agent: {Agent}", agentName);
            return Task.FromResult(existingAgent);
        }

        var agent = CreateAgentForDomain(agentName);
        _agentPool[agentName] = agent;
        _logger.LogInformation("🆕 Created agent: {Agent} (Tier {Tier})", agent.Name, agent.Tier);
        return Task.FromResult(agent);
    }

    public async Task<IAgent> CreateCustomAgentAsync(AgentSpecification specification)
    {
        var agent = new CustomAgent(
            _chatClient,
            _skillManager,
            _loggerFactory.CreateLogger<CustomAgent>(),
            specification,
            _agentMemoryService);

        _agentPool[specification.Name] = agent;
        _logger.LogInformation("🔧 Custom agent created: {Agent}", specification.Name);
        return await Task.FromResult(agent);
    }

    public AgentTier DetermineTier(ComplexityLevel complexity) => complexity switch
    {
        ComplexityLevel.Simple => AgentTier.Support,
        ComplexityLevel.Moderate => AgentTier.Specialist,
        ComplexityLevel.Complex => AgentTier.Master,
        ComplexityLevel.RequiresPlanning => AgentTier.Chief,
        _ => AgentTier.Specialist
    };

    public async Task<IEnumerable<AgentInfo>> GetAgentsByTierAsync(AgentTier tier)
    {
        var agents = _agentPool.Values
            .Where(a => a.Tier == tier && a.IsActive)
            .Select(a => new AgentInfo
            {
                Name = a.Name,
                Description = a.Description,
                Tier = a.Tier,
                Domain = a.Domain,
                CreatedAt = a.CreatedAt,
                LastUsedAt = a.LastUsedAt,
                IsActive = a.IsActive,
                AvailableTools = a.AvailableTools.ToList()
            });

        return await Task.FromResult(agents);
    }

    public async Task<IEnumerable<AgentInfo>> GetAllAgentsAsync()
    {
        var agents = _agentPool.Values
            .Where(a => a.IsActive)
            .Select(a => new AgentInfo
            {
                Name = a.Name,
                Description = a.Description,
                Tier = a.Tier,
                Domain = a.Domain,
                CreatedAt = a.CreatedAt,
                LastUsedAt = a.LastUsedAt,
                IsActive = a.IsActive,
                AvailableTools = a.AvailableTools.ToList()
            });

        return await Task.FromResult(agents);
    }

    public Task<bool> RemoveAgentAsync(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            return Task.FromResult(false);

        var removed = _agentPool.TryRemove(agentName, out _);
        if (removed)
            _logger.LogInformation("🗑️ Agent removed: {Agent}", agentName);

        return Task.FromResult(removed);
    }

    private string ResolveAgentPoolKey(string? requestedAgentName, string? fallbackDomain)
    {
        // Check estimated agent first — may be a dynamic agent name
        if (!string.IsNullOrEmpty(requestedAgentName))
        {
            // If it exists in the pool (dynamic or built-in), use it directly
            if (_agentPool.ContainsKey(requestedAgentName))
                return requestedAgentName;
        }

        // Check pool for domain-matching dynamic agents
        var domainMatch = _agentPool.Values
            .FirstOrDefault(a => a.IsActive &&
                a.Domain.Equals(fallbackDomain, StringComparison.OrdinalIgnoreCase) &&
                a is CustomAgent);

        if (domainMatch != null)
            return domainMatch.Name;

        // Fallback to built-in mapping
        if (!string.IsNullOrEmpty(requestedAgentName))
            return requestedAgentName;

        return fallbackDomain?.ToLowerInvariant() switch
        {
            "personal" => "PersonalAgent",
            "work" => "WorkAgent",
            "learning" => "LearningAgent",
            "creative" => "CreativeAgent",
            "calendar" => "CalendarAgent",
            "analysis" => "AnalysisAgent",
            "notification" => "NotificationAgent",
            "api" => "APIAgent",
            _ => "GeneralAgent"
        };
    }

    private IAgent CreateAgentForDomain(string name)
    {
        return name switch
        {
            "PersonalAgent" or "personal" => new PersonalAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<PersonalAgent>(), _agentMemoryService),
            "WorkAgent" or "work" => new WorkAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<WorkAgent>(), _agentMemoryService),
            "LearningAgent" or "learning" => new LearningAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<LearningAgent>(), _agentMemoryService),
            "CreativeAgent" or "creative" => new CreativeAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<CreativeAgent>(), _agentMemoryService),
            "CalendarAgent" or "calendar" => new CalendarAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<CalendarAgent>(), _agentMemoryService),
            "AnalysisAgent" or "analysis" => new AnalysisAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<AnalysisAgent>(), _agentMemoryService),
            "NotificationAgent" or "notification" => new NotificationAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<NotificationAgent>(), _agentMemoryService),
            "APIAgent" or "api" => new APIAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<APIAgent>(), _agentMemoryService),
            _ => new GeneralAgent(_chatClient, _skillManager, _loggerFactory.CreateLogger<GeneralAgent>(), _agentMemoryService)
        };
    }

    private void InitializeDefaultAgents()
    {
        _agentPool["PersonalAgent"] = CreateAgentForDomain("PersonalAgent");
        _agentPool["WorkAgent"] = CreateAgentForDomain("WorkAgent");
        _agentPool["LearningAgent"] = CreateAgentForDomain("LearningAgent");
        _agentPool["GeneralAgent"] = CreateAgentForDomain("GeneralAgent");
        _logger.LogInformation("🏗️ {Count} default agents initialized", _agentPool.Count);
    }
}

internal class CustomAgent : BaseAgent
{
    private readonly AgentSpecification _spec;

    public CustomAgent(
        IChatClient chatClient,
        ISkillManager skillManager,
        ILogger logger,
        AgentSpecification specification,
        IAgentMemoryService? agentMemoryService = null)
        : base(chatClient, skillManager, logger, agentMemoryService)
    {
        _spec = specification;
    }

    public override string Name => _spec.Name;
    public override string Description => _spec.Description;
    public override AgentTier Tier => _spec.Tier;
    public override string Domain => _spec.Domain;
    public override IEnumerable<string> AvailableTools => _spec.AllowedTools;

    protected override string GetBaseSystemPrompt() => _spec.Instructions;
}
