using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class DefaultExplainabilityService : IExplainabilityService
{
    private readonly IOperationalStore _operationalStore;
    private readonly ILogger<DefaultExplainabilityService> _logger;

    public DefaultExplainabilityService(
        IOperationalStore operationalStore,
        ILogger<DefaultExplainabilityService> logger)
    {
        _operationalStore = operationalStore;
        _logger = logger;
    }

    public async Task<DecisionExplanation> ExplainDecisionAsync(string decisionId, CancellationToken ct = default)
    {
        _logger.LogInformation("🕵️ Generating explanation for decision: {DecisionId}", decisionId);

        var artifacts = await _operationalStore.GetArtifactsAsync(decisionId, ct);
        
        var reasoningChain = new List<ReasoningStep>();
        var sourcesUsed = new List<string>();
        int order = 1;

        foreach (var artifact in artifacts)
        {
            reasoningChain.Add(new ReasoningStep
            {
                Order = order++,
                Title = artifact.Name,
                Description = artifact.Summary ?? "Processed artifact.",
                StepType = artifact.Type.ToString()
            });

            if (artifact.Type == AgentExecutionArtifactType.RagContext)
            {
                sourcesUsed.Add(artifact.Name);
            }
        }

        return new DecisionExplanation
        {
            DecisionId = decisionId,
            AgentName = artifacts.FirstOrDefault(a => !string.IsNullOrEmpty(a.AgentName))?.AgentName ?? "Unknown",
            ReasoningChain = reasoningChain,
            SourcesUsed = sourcesUsed,
            Timestamp = DateTime.UtcNow
        };
    }

    public Task<DecisionExplanation> GenerateExplanationAsync(string agentName, string question, string answer, CancellationToken ct = default)
    {
        return Task.FromResult(new DecisionExplanation
        {
            AgentName = agentName,
            Question = question,
            Answer = answer,
            ReasoningChain = new List<ReasoningStep>
            {
                new() { Order = 1, Title = "Input Analysis", Description = "Analyzing the user's intent and complexity.", StepType = "Pre-processing" },
                new() { Order = 2, Title = "Knowledge Retrieval", Description = "Searching the knowledge base for relevant facts.", StepType = "RAG" },
                new() { Order = 3, Title = "Answer Synthesis", Description = "Composing a grounded response using the retrieved information.", StepType = "Execution" }
            },
            Timestamp = DateTime.UtcNow
        });
    }
}
