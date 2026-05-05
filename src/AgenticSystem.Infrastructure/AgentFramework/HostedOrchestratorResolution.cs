namespace AgenticSystem.Infrastructure.AgentFramework;

internal sealed class HostedOrchestratorResolution
{
    public const string AgentName = "Orchestrator";

    public HostedOrchestratorResolution(OrchestratorContext context)
    {
        Context = context;
    }

    public OrchestratorContext Context { get; }
}