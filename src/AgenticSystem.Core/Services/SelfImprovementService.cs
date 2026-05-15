using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Core.Services;

public class SelfImprovementService : ISelfImprovementEngine
{
    private readonly IOperationalStore _operationalStore;
    private readonly IAgentVersioningService _versioningService;
    private readonly ILogger<SelfImprovementService> _logger;
    private readonly SelfImprovementSettings _settings;
    private const string CursorKey = "SelfImprovement_LastReflectionId";

    public SelfImprovementService(
        IOperationalStore operationalStore,
        IAgentVersioningService versioningService,
        IOptions<SelfImprovementSettings> options,
        ILogger<SelfImprovementService> logger)
    {
        _operationalStore = operationalStore;
        _versioningService = versioningService;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task ProcessBatchImprovementsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("⏳ Starting daily batch self-improvement cycle...");

        // 1. Get cursor
        var cursorState = await _operationalStore.GetSystemStateAsync(CursorKey, ct);
        var lastId = cursorState?.Value;

        // 2. Fetch new reflections
        var newReflections = await _operationalStore.GetReflectionsSinceAsync(lastId, 500, ct);
        
        if (!newReflections.Any())
        {
            _logger.LogInformation("✅ No new reflections to process.");
            return;
        }

        // 3. Group by agent
        var agentsToProcess = newReflections
            .Where(r => r.Severity == ReflectionSeverity.Critical)
            .GroupBy(r => r.AgentName)
            .ToList();

        _logger.LogInformation("🔍 Found critical reflections for {Count} agents.", agentsToProcess.Count);

        foreach (var group in agentsToProcess)
        {
            var agentName = group.Key;
            var record = await AnalyzeAndImproveAsync(agentName, ct);

            if (record.Status == "Proposed")
            {
                // Auto-apply logic
                if (record.ConfidenceLevel >= _settings.AutoApplyThreshold)
                {
                    _logger.LogInformation("🤖 Confidence {Level:P0} meets threshold ({Threshold:P0}). Auto-applying for {AgentName}.", 
                        record.ConfidenceLevel, _settings.AutoApplyThreshold, agentName);
                    record.IsAutoApplied = true;
                    await ApplyImprovementAsync(record.Id, ct);
                    record.Status = "Applied";
                }
            }
        }

        // 4. Update cursor
        var latestReflectionId = newReflections.Last().Id;
        await _operationalStore.SaveSystemStateAsync(new SystemState 
        { 
            Id = CursorKey, 
            Value = latestReflectionId 
        }, ct);

        _logger.LogInformation("🏁 Batch cycle completed. Cursor updated to: {Id}", latestReflectionId);
    }

    public async Task<SelfImprovementRecord> AnalyzeAndImproveAsync(string agentName, CancellationToken ct = default)
    {
        _logger.LogInformation("🔄 Analyzing performance for agent: {AgentName}", agentName);

        // Fetch recent learnings (limit for analysis)
        var reflections = await _operationalStore.GetRecentLearningsAsync(50, ct);
        var agentReflections = reflections.Where(r => r.AgentName == agentName).ToList();
        
        var criticalReflections = agentReflections.Where(r => r.Severity == ReflectionSeverity.Critical).ToList();
        
        var record = new SelfImprovementRecord
        {
            AgentName = agentName,
            Type = ImprovementType.PromptRefinement,
            Rationale = $"Analyzing {criticalReflections.Count} critical reflections."
        };

        if (criticalReflections.Any())
        {
            // Simple heuristic for confidence: based on volume of critical lessons
            // More lessons for the same agent = more confidence that a fix is needed.
            record.ConfidenceLevel = Math.Min(0.5 + (criticalReflections.Count * 0.1), 0.95);
            
            record.ProposedChanges["instructions_update"] = "Refine constraints based on lessons: " + 
                string.Join("; ", criticalReflections.SelectMany(r => r.LessonsLearned).Distinct().Take(5));
                
            record.Status = "Proposed";
        }
        else
        {
            record.Status = "NoImprovementNeeded";
            record.ConfidenceLevel = 1.0;
            record.Rationale = "Current performance is stable.";
        }

        return record;
    }

    public async Task<bool> ApplyImprovementAsync(string improvementId, CancellationToken ct = default)
    {
        _logger.LogInformation("🚀 Applying self-improvement: {Id}", improvementId);
        // Em uma implementação real, chamaria _versioningService.CreateNewSnapshotAsync(...)
        return await Task.FromResult(true);
    }
}
