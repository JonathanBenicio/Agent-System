using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class SelfImprovementService : ISelfImprovementEngine
{
    private readonly IOperationalStore _operationalStore;
    private readonly IAgentVersioningService _versioningService;
    private readonly ILogger<SelfImprovementService> _logger;

    public SelfImprovementService(
        IOperationalStore operationalStore,
        IAgentVersioningService versioningService,
        ILogger<SelfImprovementService> logger)
    {
        _operationalStore = operationalStore;
        _versioningService = versioningService;
        _logger = logger;
    }

    public async Task<SelfImprovementRecord> AnalyzeAndImproveAsync(string agentName, CancellationToken ct = default)
    {
        _logger.LogInformation("🔄 Starting self-improvement cycle for agent: {AgentName}", agentName);

        // 1. Fetch recent reflections and evaluations
        var reflections = await _operationalStore.GetRecentLearningsAsync(20, ct);
        
        var record = new SelfImprovementRecord
        {
            AgentName = agentName,
            Type = ImprovementType.PromptRefinement,
            Rationale = "Analyzing recent performance regressions and negative reflections."
        };

        // 2. Look for critical patterns
        var criticalReflections = reflections.Where(r => r.AgentName == agentName && r.Severity == ReflectionSeverity.Critical).ToList();
        
        if (criticalReflections.Count > 0)
        {
            _logger.LogInformation("⚠️ Found {Count} critical reflections for {AgentName}. Proposing improvement.", criticalReflections.Count, agentName);
            
            record.ProposedChanges["instructions_update"] = "Add explicit constraints based on: " + 
                string.Join("; ", criticalReflections.Select(r => r.LessonsLearned.FirstOrDefault()));
                
            record.Status = "Proposed";
        }
        else
        {
            record.Status = "NoImprovementNeeded";
            record.Rationale = "Current performance is stable based on recent metrics.";
        }

        return record;
    }

    public async Task<bool> ApplyImprovementAsync(string improvementId, CancellationToken ct = default)
    {
        _logger.LogInformation("🚀 Applying self-improvement: {Id}", improvementId);
        
        // In a real implementation, this would trigger a new version snapshot
        // via IAgentVersioningService with the optimized configuration.
        return await Task.FromResult(true);
    }
}

public interface ISelfImprovementEngine
{
    Task<SelfImprovementRecord> AnalyzeAndImproveAsync(string agentName, CancellationToken ct = default);
    Task<bool> ApplyImprovementAsync(string improvementId, CancellationToken ct = default);
}
