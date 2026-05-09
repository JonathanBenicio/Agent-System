using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentSimulationService : IAgentSimulationEngine
{
    private readonly IDirectAgentRequestExecutor _agentExecutor;
    private readonly ILogger<AgentSimulationService> _logger;
    private readonly List<SimulationResult> _history = new();

    public AgentSimulationService(
        IDirectAgentRequestExecutor agentExecutor,
        ILogger<AgentSimulationService> logger)
    {
        _agentExecutor = agentExecutor;
        _logger = logger;
    }

    public async Task<SimulationResult> RunSimulationAsync(SimulationConfig config, CancellationToken ct = default)
    {
        _logger.LogInformation("🧪 Running simulation for agent: {AgentName}", config.DryRun ? "(Dry Run)" : "(Full)");

        var actions = new List<SimulatedAction>();
        bool wouldSucceed = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Placeholder simulation logic: using one of the mock responses if provided
            var input = config.MockResponses.Keys.FirstOrDefault() ?? "Test input";
            
            var context = new UserContext { UserId = "simulator", TenantId = "simulation" };
            var response = await _agentExecutor.ExecuteAsync("sim_run", input, context, "MetaAgent", ct);
            
            actions.Add(new SimulatedAction
            {
                Sequence = 1,
                ActionType = "llm_call",
                Description = $"Executing sim for {input}",
                EstimatedCostUsd = 0.01,
                MockResult = response.Content
            });

            wouldSucceed = response.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulation failed");
            wouldSucceed = false;
        }

        sw.Stop();

        var result = new SimulationResult
        {
            SimulationId = Guid.NewGuid().ToString("N"),
            Actions = actions,
            EstimatedDuration = sw.Elapsed,
            EstimatedTokens = 150,
            WouldSucceed = wouldSucceed
        };

        lock (_history)
        {
            _history.Insert(0, result);
            if (_history.Count > 100) _history.RemoveAt(_history.Count - 1);
        }

        return result;
    }

    public Task<IReadOnlyList<SimulationResult>> GetSimulationHistoryAsync(string? agentName = null, int limit = 10, CancellationToken ct = default)
    {
        lock (_history)
        {
            return Task.FromResult<IReadOnlyList<SimulationResult>>(_history.Take(limit).ToList());
        }
    }
}
