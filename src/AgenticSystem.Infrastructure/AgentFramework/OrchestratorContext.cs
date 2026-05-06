namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Contexto do orquestrador montado: o agent do framework + os bindings dos especialistas.
/// </summary>
public sealed record OrchestratorContext(
    Microsoft.Agents.AI.AIAgent OrchestratorAgent,
    List<AgentToolBinding> SpecialistBindings);