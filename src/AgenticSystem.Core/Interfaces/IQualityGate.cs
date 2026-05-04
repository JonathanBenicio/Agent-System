namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Quality Gate — validação pré/pós execução de requests.
/// </summary>
public interface IQualityGate
{
    string Name { get; }
    int Order { get; }
    QualityGatePhase Phase { get; }

    Task<QualityResult> ValidateAsync(QualityContext context, CancellationToken ct = default);
}

/// <summary>
/// Orquestrador de quality gates.
/// </summary>
public interface IQualityGateService
{
    Task<QualityReport> ValidateRequestAsync(string input, Dictionary<string, object>? metadata = null, CancellationToken ct = default);
    Task<QualityReport> ValidateResponseAsync(string input, string output, Dictionary<string, object>? metadata = null, CancellationToken ct = default);
    void RegisterGate(IQualityGate gate);
    IEnumerable<IQualityGate> GetRegisteredGates();
}

// ═══════════════════════════════════════════════════════════
// Quality Models
// ═══════════════════════════════════════════════════════════

public enum QualityGatePhase
{
    PreExecution,
    PostExecution
}

public class QualityContext
{
    public string Input { get; set; } = string.Empty;
    public string? Output { get; set; }
    public QualityGatePhase Phase { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class QualityResult
{
    public string GateName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public double Score { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();

    public static QualityResult Pass(string gateName, double score = 10)
        => new() { GateName = gateName, Passed = true, Score = score };

    public static QualityResult Fail(string gateName, double score, params string[] issues)
        => new() { GateName = gateName, Passed = false, Score = score, Issues = issues.ToList() };
}

public class QualityReport
{
    public bool OverallPassed { get; set; }
    public double AverageScore { get; set; }
    public QualityGatePhase Phase { get; set; }
    public List<QualityResult> Results { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
