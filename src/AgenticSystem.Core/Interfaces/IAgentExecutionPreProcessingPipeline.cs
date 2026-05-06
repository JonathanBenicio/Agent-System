using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IAgentExecutionPreProcessingPipeline
{
    Task<AgentExecutionPreProcessingResult> ProcessAsync(
        AgentExecutionPreProcessingContext context,
        CancellationToken ct = default);
}