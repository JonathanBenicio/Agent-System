namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Metadata estável do orquestrador hospedado.
/// </summary>
public sealed record OrchestratorMetadata(string Name, string Description)
{
    public static OrchestratorMetadata Default { get; } = new(
        "Orchestrator",
        "Agente orquestrador que coordena especialistas via tool calling");
}