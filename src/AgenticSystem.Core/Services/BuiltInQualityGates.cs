using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Quality Gate: valida que o input não está vazio e tem tamanho razoável.
/// </summary>
public class InputValidationGate : IQualityGate
{
    public string Name => "InputValidation";
    public int Order => 1;
    public QualityGatePhase Phase => QualityGatePhase.PreExecution;

    public Task<QualityResult> ValidateAsync(QualityContext context, CancellationToken ct = default)
    {
        var issues = new List<string>();
        double score = 10;

        if (string.IsNullOrWhiteSpace(context.Input))
        {
            issues.Add("Input is empty or whitespace");
            return Task.FromResult(QualityResult.Fail(Name, 0, issues.ToArray()));
        }

        if (context.Input.Length < 2)
        {
            issues.Add("Input is too short (< 2 chars)");
            score -= 3;
        }

        if (context.Input.Length > 50000)
        {
            issues.Add("Input exceeds 50K characters");
            score -= 5;
        }

        return Task.FromResult(issues.Count > 0
            ? QualityResult.Fail(Name, score, issues.ToArray())
            : QualityResult.Pass(Name, score));
    }
}

/// <summary>
/// Quality Gate: valida que a resposta é relevante e não está vazia.
/// </summary>
public class ResponseQualityGate : IQualityGate
{
    public string Name => "ResponseQuality";
    public int Order => 1;
    public QualityGatePhase Phase => QualityGatePhase.PostExecution;

    public Task<QualityResult> ValidateAsync(QualityContext context, CancellationToken ct = default)
    {
        var issues = new List<string>();
        var suggestions = new List<string>();
        double score = 10;

        if (string.IsNullOrWhiteSpace(context.Output))
        {
            issues.Add("Output is empty");
            return Task.FromResult(QualityResult.Fail(Name, 0, issues.ToArray()));
        }

        if (context.Output!.Length < 10)
        {
            issues.Add("Output is suspiciously short (< 10 chars)");
            score -= 3;
        }

        if (context.Output.Contains("error", StringComparison.OrdinalIgnoreCase) &&
            context.Output.Contains("sorry", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Response may contain an error acknowledgment");
            score -= 1;
        }

        var result = score >= 7
            ? QualityResult.Pass(Name, score)
            : QualityResult.Fail(Name, score, issues.ToArray());

        result.Suggestions = suggestions;
        return Task.FromResult(result);
    }
}
