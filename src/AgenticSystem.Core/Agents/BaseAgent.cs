using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Agents;

public abstract class BaseAgent : IAgent
{
    private readonly ISkillManager _skillManager;
    private readonly IAgentMemoryService? _agentMemoryService;
    private readonly ILogger _logger;

    protected BaseAgent(
        ISkillManager skillManager,
        ILogger logger,
        IAgentMemoryService? agentMemoryService = null)
    {
        _skillManager = skillManager;
        _agentMemoryService = agentMemoryService;
        _logger = logger;
        CreatedAt = DateTime.UtcNow;
        LastUsedAt = DateTime.UtcNow;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract AgentTier Tier { get; }
    public abstract string Domain { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastUsedAt { get; private set; }
    public bool IsActive { get; set; } = true;
    public virtual IEnumerable<string> AvailableTools => Enumerable.Empty<string>();
    
    public virtual string Instructions => GetBaseSystemPrompt();

    public virtual async Task<bool> CanHandleAsync(AnalysisResult analysis)
    {
        var domainMatch = analysis.PrimaryDomain.Equals(Domain, StringComparison.OrdinalIgnoreCase) ||
                          analysis.SecondaryDomains.Contains(Domain, StringComparer.OrdinalIgnoreCase);
        var tierMatch = analysis.RecommendedTier >= Tier;
        return await Task.FromResult(domainMatch && tierMatch);
    }

    public void UpdateLastUsed() => LastUsedAt = DateTime.UtcNow;

    protected abstract string GetBaseSystemPrompt();
}
