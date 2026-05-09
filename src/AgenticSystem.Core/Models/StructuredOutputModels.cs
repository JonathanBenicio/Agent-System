using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Structured Output — JSON Schema Enforcement
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Defines the expected output schema for an agent response.
/// Used for contract-first output validation.
/// </summary>
public class StructuredOutputSchema
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// JSON Schema definition as a string (follows JSON Schema Draft 7+).
    /// </summary>
    public string SchemaJson { get; init; } = "{}";

    /// <summary>
    /// Required top-level fields that must be present in the output.
    /// </summary>
    public List<string> RequiredFields { get; init; } = [];

    /// <summary>
    /// If true, the system will automatically retry on validation failure.
    /// </summary>
    public bool AutoRetryOnFailure { get; init; } = true;

    /// <summary>
    /// Maximum number of retries for auto-fix.
    /// </summary>
    public int MaxRetries { get; init; } = 2;
}

/// <summary>
/// Result of validating an agent output against a schema.
/// </summary>
public class StructuredOutputValidationResult
{
    public bool IsValid { get; init; }
    public string? RawOutput { get; init; }
    public JsonDocument? ParsedOutput { get; init; }
    public List<string> ValidationErrors { get; init; } = [];
    public int AttemptNumber { get; init; } = 1;

    public static StructuredOutputValidationResult Success(string raw, JsonDocument parsed, int attempt = 1) => new()
    {
        IsValid = true,
        RawOutput = raw,
        ParsedOutput = parsed,
        AttemptNumber = attempt
    };

    public static StructuredOutputValidationResult Failure(string raw, List<string> errors, int attempt = 1) => new()
    {
        IsValid = false,
        RawOutput = raw,
        ValidationErrors = errors,
        AttemptNumber = attempt
    };
}
