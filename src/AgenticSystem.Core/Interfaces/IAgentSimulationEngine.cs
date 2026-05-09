using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Engine for running agent simulations to test behavior and safety.
/// </summary>
public interface IAgentSimulationEngine
{
    Task<SimulationResult> RunSimulationAsync(SimulationConfig config, CancellationToken ct = default);
    Task<IReadOnlyList<SimulationResult>> GetSimulationHistoryAsync(string? agentName = null, int limit = 10, CancellationToken ct = default);
}

// Model reconciliation: SimulationResult is now used from AgenticSystem.Core.Models.AutonomyAndRiskModels.cs
