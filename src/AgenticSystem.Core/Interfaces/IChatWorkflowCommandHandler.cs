using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Handles workflow commands issued through the conversational chat interface
/// (e.g., "iniciar workflow X", "cancelar workflow Y", "listar workflows").
/// Isolates this parsing/execution logic from MetaAgentOrchestrator.
/// </summary>
public interface IChatWorkflowCommandHandler
{
    /// <summary>
    /// Attempts to match and execute a workflow command from the chat input.
    /// Returns null if the input is not a recognized workflow command.
    /// </summary>
    Task<AgentResponse?> TryHandleAsync(string input, UserContext context, CancellationToken ct = default);
}
