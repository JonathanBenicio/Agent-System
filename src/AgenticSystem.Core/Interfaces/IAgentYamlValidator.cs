using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public class YamlValidationError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error"; // "Error" ou "Warning"
}

public class YamlValidationResult
{
    public bool IsValid { get; set; }
    public List<YamlValidationError> Errors { get; set; } = new();
    public AgentSpecification? Specification { get; set; }
}

public interface IAgentYamlValidator
{
    Task<YamlValidationResult> ValidateAsync(string yaml, CancellationToken ct = default);
}
