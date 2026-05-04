using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

public class QualityGateService : IQualityGateService
{
    private readonly List<IQualityGate> _gates = new();
    private readonly ILogger<QualityGateService> _logger;

    public QualityGateService(IEnumerable<IQualityGate> gates, ILogger<QualityGateService> logger)
    {
        _gates.AddRange(gates);
        _logger = logger;
    }

    public async Task<QualityReport> ValidateRequestAsync(string input, Dictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        var context = new QualityContext
        {
            Input = input,
            Phase = QualityGatePhase.PreExecution,
            Metadata = metadata ?? new()
        };

        return await RunGatesAsync(context, QualityGatePhase.PreExecution, ct);
    }

    public async Task<QualityReport> ValidateResponseAsync(string input, string output, Dictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        var context = new QualityContext
        {
            Input = input,
            Output = output,
            Phase = QualityGatePhase.PostExecution,
            Metadata = metadata ?? new()
        };

        return await RunGatesAsync(context, QualityGatePhase.PostExecution, ct);
    }

    public void RegisterGate(IQualityGate gate) => _gates.Add(gate);
    public IEnumerable<IQualityGate> GetRegisteredGates() => _gates.AsReadOnly();

    private async Task<QualityReport> RunGatesAsync(QualityContext context, QualityGatePhase phase, CancellationToken ct)
    {
        var gates = _gates
            .Where(g => g.Phase == phase)
            .OrderBy(g => g.Order)
            .ToList();

        var results = new List<QualityResult>();

        foreach (var gate in gates)
        {
            try
            {
                var result = await gate.ValidateAsync(context, ct);
                results.Add(result);
                _logger.LogDebug("Quality gate '{Gate}': {Status} (score: {Score})",
                    gate.Name, result.Passed ? "PASSED" : "FAILED", result.Score);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Quality gate '{Gate}' threw an exception", gate.Name);
                results.Add(QualityResult.Fail(gate.Name, 0, $"Gate error: {ex.Message}"));
            }
        }

        return new QualityReport
        {
            Phase = phase,
            Results = results,
            OverallPassed = results.All(r => r.Passed),
            AverageScore = results.Count > 0 ? results.Average(r => r.Score) : 10
        };
    }
}
