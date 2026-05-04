using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Agents;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

public class HierarchicalAgentFactory : IAgentFactory
{
    private readonly ConcurrentDictionary<string, IAgent> _agentPool = new();
    private readonly ILLMManager _llmManager;
    private readonly ISkillManager _skillManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HierarchicalAgentFactory> _logger;

    public HierarchicalAgentFactory(
        ILLMManager llmManager,
        ISkillManager skillManager,
        ILoggerFactory loggerFactory,
        ILogger<HierarchicalAgentFactory> logger)
    {
        _llmManager = llmManager;
        _skillManager = skillManager;
        _loggerFactory = loggerFactory;
        _logger = logger;
        InitializeDefaultAgents();
    }

    public async Task<IAgent> GetOrCreateAgentAsync(AnalysisResult context)
    {
        var agentName = ResolveAgentName(context);

        if (_agentPool.TryGetValue(agentName, out var existingAgent) && existingAgent.IsActive)
        {
            _logger.LogDebug("♻️ Reusing agent: {Agent}", agentName);
            return existingAgent;
        }

        var agent = CreateAgentForDomain(agentName);
        _agentPool[agentName] = agent;
        _logger.LogInformation("🆕 Created agent: {Agent} (Tier {Tier})", agent.Name, agent.Tier);
        return await Task.FromResult(agent);
    }

    public async Task<IAgent> CreateCustomAgentAsync(AgentSpecification specification)
    {
        var agent = new CustomAgent(
            _llmManager,
            _skillManager,
            _loggerFactory.CreateLogger<CustomAgent>(),
            specification);

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

    private string ResolveAgentName(AnalysisResult context)
    {
        // Check estimated agent first — may be a dynamic agent name
        if (!string.IsNullOrEmpty(context.EstimatedAgent))
        {
            // If it exists in the pool (dynamic or built-in), use it directly
            if (_agentPool.ContainsKey(context.EstimatedAgent))
                return context.EstimatedAgent;
        }

        // Check pool for domain-matching dynamic agents
        var domainMatch = _agentPool.Values
            .FirstOrDefault(a => a.IsActive &&
                a.Domain.Equals(context.PrimaryDomain, StringComparison.OrdinalIgnoreCase) &&
                a is CustomAgent);

        if (domainMatch != null)
            return domainMatch.Name;

        // Fallback to built-in mapping
        if (!string.IsNullOrEmpty(context.EstimatedAgent))
            return context.EstimatedAgent;

        return context.PrimaryDomain?.ToLowerInvariant() switch
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
            "PersonalAgent" or "personal" => new PersonalAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<PersonalAgent>()),
            "WorkAgent" or "work" => new WorkAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<WorkAgent>()),
            "LearningAgent" or "learning" => new LearningAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<LearningAgent>()),
            "CreativeAgent" or "creative" => new CreativeAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<CreativeAgent>()),
            "CalendarAgent" or "calendar" => new CalendarAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<CalendarAgent>()),
            "AnalysisAgent" or "analysis" => new AnalysisAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<AnalysisAgent>()),
            "NotificationAgent" or "notification" => new NotificationAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<NotificationAgent>()),
            "APIAgent" or "api" => new APIAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<APIAgent>()),
            _ => new GeneralAgent(_llmManager, _skillManager, _loggerFactory.CreateLogger<GeneralAgent>())
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
        ILLMManager llmManager,
        ISkillManager skillManager,
        ILogger logger,
        AgentSpecification specification)
        : base(llmManager, skillManager, logger)
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
