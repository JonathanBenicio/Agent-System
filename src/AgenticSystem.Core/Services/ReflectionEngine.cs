using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 4 — Reflexão pós-ação: avalia resultado, confiança, desvios.
/// </summary>
public class ReflectionEngine : IReflectionEngine
{
    private readonly ConcurrentBag<Reflection> _reflections = new();
    private readonly ILogger<ReflectionEngine> _logger;

    public ReflectionEngine(ILogger<ReflectionEngine> logger)
    {
        _logger = logger;
    }

    public Task<Reflection> ReflectAsync(string sessionId, string agentName, string action, string outcome, double confidence)
    {
        var reflection = new Reflection
        {
            SessionId = sessionId,
            AgentName = agentName,
            ActionTaken = action,
            Outcome = outcome,
            ConfidenceInOutcome = confidence
        };

        // Detect deviations based on confidence
        if (confidence < 0.5)
        {
            reflection.Deviations.Add("Low confidence in outcome — result may be imprecise");
            reflection.Severity = ReflectionSeverity.Warning;
        }

        if (confidence < 0.3)
        {
            reflection.Deviations.Add("Very low confidence — human review recommended");
            reflection.Severity = ReflectionSeverity.Critical;
            reflection.ImprovementSuggestion = "Consider providing more context or breaking the task into smaller steps";
        }

        // Generate lessons learned based on outcome analysis
        if (outcome.Length > 500)
        {
            reflection.LessonsLearned.Add("Complex response generated — consider semantic compression for future reference");
        }

        if (confidence >= 0.8)
        {
            reflection.LessonsLearned.Add($"High confidence response by {agentName} — pattern can be reinforced");
        }

        _reflections.Add(reflection);
        _logger.LogDebug("Reflection created for session {SessionId}: confidence={Confidence:F2}, severity={Severity}",
            sessionId, confidence, reflection.Severity);

        return Task.FromResult(reflection);
    }

    public Task<IEnumerable<Reflection>> GetSessionReflectionsAsync(string sessionId)
    {
        var reflections = _reflections
            .Where(r => r.SessionId == sessionId)
            .OrderByDescending(r => r.CreatedAt)
            .AsEnumerable();

        return Task.FromResult(reflections);
    }

    public Task<IEnumerable<Reflection>> GetRecentLearningsAsync(int count = 10)
    {
        var learnings = _reflections
            .Where(r => r.LessonsLearned.Count > 0)
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .AsEnumerable();

        return Task.FromResult(learnings);
    }
}
