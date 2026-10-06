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
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IAgent>> _tenantAgentPools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISkillManager _skillManager;
    private readonly IAgentMemoryService? _agentMemoryService;
    private readonly IDynamicAgentRepository _dynamicAgentRepository;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HierarchicalAgentFactory> _logger;
    private readonly ITenantContextAccessor? _tenantContextAccessor;

    public HierarchicalAgentFactory(
        ISkillManager skillManager,
        IDynamicAgentRepository dynamicAgentRepository,
        ILoggerFactory loggerFactory,
        ILogger<HierarchicalAgentFactory> logger,
        IAgentMemoryService? agentMemoryService = null,
        ITenantContextAccessor? tenantContextAccessor = null)
    {
        _skillManager = skillManager;
        _dynamicAgentRepository = dynamicAgentRepository;
        _agentMemoryService = agentMemoryService;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
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

    private async Task<IAgent> ResolveAgentByIdentityAsync(string? requestedAgentName, string? fallbackDomain)
    {
        var agentName = ResolveAgentPoolKey(requestedAgentName, fallbackDomain);
        var tenantPool = GetTenantAgentPool();

        var dynamicSpec = await _dynamicAgentRepository.GetByNameAsync(agentName);
        if (dynamicSpec != null)
        {
            var dynamicAgent = CreateCustomAgent(dynamicSpec);
            tenantPool[dynamicSpec.Name] = dynamicAgent;
            return dynamicAgent;
        }

        tenantPool.TryRemove(agentName, out _);
        if (_agentPool.TryGetValue(agentName, out var existingAgent) && existingAgent.IsActive)
            return existingAgent;

        var agent = CreateAgentForDomain(agentName);
        _agentPool[agentName] = agent;
        _logger.LogInformation("🆕 Created agent: {Agent} (Tier {Tier})", agent.Name, agent.Tier);
        return agent;
    }

    public async Task<IAgent> CreateCustomAgentAsync(AgentSpecification specification)
    {
        var agent = CreateCustomAgent(specification);

        // Persist to DB
        await _dynamicAgentRepository.SaveAsync(specification);

        GetTenantAgentPool()[specification.Name] = agent;
        _logger.LogInformation("🔧 Custom agent created and persisted: {Agent}", specification.Name);
        return agent;
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
        await EnsureDynamicAgentsLoadedAsync();

        var agents = CurrentAgentPoolValues()
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
                AutonomyLevel = a.AutonomyLevel,
                AvailableTools = a.AvailableTools.ToList(),
                Instructions = a.Instructions,
                Capabilities = a is CustomAgent custom ? custom.Specification.Capabilities.ToList() : [],
                Configuration = a is CustomAgent configured ? new(configured.Specification.Configuration) : new()
            });

        return agents;
    }

    public async Task<IEnumerable<AgentInfo>> GetAllAgentsAsync()
    {
        await EnsureDynamicAgentsLoadedAsync();

        var agents = CurrentAgentPoolValues()
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
                AutonomyLevel = a.AutonomyLevel,
                AvailableTools = a.AvailableTools.ToList(),
                Instructions = a.Instructions,
                Capabilities = a is CustomAgent custom ? custom.Specification.Capabilities.ToList() : [],
                Configuration = a is CustomAgent configured ? new(configured.Specification.Configuration) : new()
            });

        return agents;
    }

    private async Task EnsureDynamicAgentsLoadedAsync()
    {
        var dynamicSpecs = await _dynamicAgentRepository.GetAllAsync();
        var tenantPool = GetTenantAgentPool();
        var activeNames = new HashSet<string>(dynamicSpecs.Select(spec => spec.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var spec in dynamicSpecs)
        {
            tenantPool[spec.Name] = CreateCustomAgent(spec);
        }

        foreach (var cachedName in tenantPool.Keys)
            if (!activeNames.Contains(cachedName)) tenantPool.TryRemove(cachedName, out _);
    }


    public async Task<bool> RemoveAgentAsync(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            return false;

        var removed = GetTenantAgentPool().TryRemove(agentName, out _);
        var dbRemoved = await _dynamicAgentRepository.DeactivateAsync(agentName);

        if (removed || dbRemoved)
            _logger.LogInformation("🗑️ Agent removed/deactivated: {Agent}", agentName);

        return removed || dbRemoved;
    }

    private string ResolveAgentPoolKey(string? requestedAgentName, string? fallbackDomain)
    {
        // Check estimated agent first — may be a dynamic agent name
        if (!string.IsNullOrEmpty(requestedAgentName))
        {
            // If it exists in the pool (dynamic or built-in), use it directly
            if (_agentPool.ContainsKey(requestedAgentName)
                || GetTenantAgentPool().ContainsKey(requestedAgentName))
                return requestedAgentName;
        }

        // Check pool for domain-matching dynamic agents
        var domainMatch = GetTenantAgentPool().Values
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
            "dotnet" or "dotnet-expert" or "dotnet-self-learning-architect" => "DotNetExpertAgent",
            "workflow" or "automator" => "WorkflowSpecialist",
            _ => "GeneralAgent"
        };
    }

    private ConcurrentDictionary<string, IAgent> GetTenantAgentPool()
    {
        var tenantId = "__unscoped__";
        try
        {
            if (_tenantContextAccessor is not null)
                tenantId = _tenantContextAccessor.CurrentTenantId;
        }
        catch (InvalidOperationException)
        {
            // Unit and in-memory usages without tenant middleware remain isolated in a dedicated partition.
        }

        return _tenantAgentPools.GetOrAdd(tenantId, static _ => new ConcurrentDictionary<string, IAgent>(StringComparer.OrdinalIgnoreCase));
    }

    private IEnumerable<IAgent> CurrentAgentPoolValues()
    {
        var tenantAgents = GetTenantAgentPool().Values.ToList();
        var tenantAgentNames = tenantAgents.Select(agent => agent.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tenantAgents.Concat(_agentPool.Values.Where(agent => !tenantAgentNames.Contains(agent.Name)));
    }

    private CustomAgent CreateCustomAgent(AgentSpecification specification) => new(
        _skillManager,
        _loggerFactory.CreateLogger<CustomAgent>(),
        specification,
        _agentMemoryService);

    private IAgent CreateAgentForDomain(string name)
    {
        return name switch
        {
            "PersonalAgent" or "personal" => new PersonalAgent(_skillManager, _loggerFactory.CreateLogger<PersonalAgent>(), _agentMemoryService),
            "WorkAgent" or "work" => new WorkAgent(_skillManager, _loggerFactory.CreateLogger<WorkAgent>(), _agentMemoryService),
            "LearningAgent" or "learning" => new LearningAgent(_skillManager, _loggerFactory.CreateLogger<LearningAgent>(), _agentMemoryService),
            "CreativeAgent" or "creative" => new CreativeAgent(_skillManager, _loggerFactory.CreateLogger<CreativeAgent>(), _agentMemoryService),
            "CalendarAgent" or "calendar" => new CalendarAgent(_skillManager, _loggerFactory.CreateLogger<CalendarAgent>(), _agentMemoryService),
            "AnalysisAgent" or "analysis" => new AnalysisAgent(_skillManager, _loggerFactory.CreateLogger<AnalysisAgent>(), _agentMemoryService),
            "NotificationAgent" or "notification" => new NotificationAgent(_skillManager, _loggerFactory.CreateLogger<NotificationAgent>(), _agentMemoryService),
            "APIAgent" or "api" => new APIAgent(_skillManager, _loggerFactory.CreateLogger<APIAgent>(), _agentMemoryService),
            "DotNetExpertAgent" or "dotnet" => new DotNetExpertAgent(_skillManager, _loggerFactory.CreateLogger<DotNetExpertAgent>(), _agentMemoryService),
            "WorkflowSpecialist" or "workflow" => new WorkflowSpecialist(_skillManager, _loggerFactory.CreateLogger<WorkflowSpecialist>(), _agentMemoryService),
            _ => new GeneralAgent(_skillManager, _loggerFactory.CreateLogger<GeneralAgent>(), _agentMemoryService)
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
        ISkillManager skillManager,
        ILogger logger,
        AgentSpecification specification,
        IAgentMemoryService? agentMemoryService = null)
        : base(skillManager, logger, agentMemoryService)
    {
        _spec = specification;
    }

    internal AgentSpecification Specification => _spec;
    public override string Name => _spec.Name;
    public override string Description => _spec.Description;
    public override AgentTier Tier => _spec.Tier;
    public override string Domain => _spec.Domain;
    public override AutonomyLevel AutonomyLevel => _spec.AutonomyLevel;
    public override IEnumerable<string> AvailableTools => _spec.AllowedTools;

    protected override string GetBaseSystemPrompt() => _spec.Instructions;
}
