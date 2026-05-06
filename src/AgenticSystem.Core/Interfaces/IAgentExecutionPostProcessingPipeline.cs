using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IAgentExecutionPostProcessingPipeline
{
    Task<AgentResponse> ProcessAsync(
        AgentExecutionPostProcessingContext context,
        CancellationToken ct = default);
}