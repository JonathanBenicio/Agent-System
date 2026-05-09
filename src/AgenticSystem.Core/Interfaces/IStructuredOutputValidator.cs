using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Validates agent outputs against a structured schema.
/// Supports JSON parsing, field validation, and auto-retry hints.
/// </summary>
public interface IStructuredOutputValidator
{
    /// <summary>
    /// Validates raw output against the given schema.
    /// </summary>
    Task<StructuredOutputValidationResult> ValidateAsync(
        string rawOutput,
        StructuredOutputSchema schema,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts and validates a JSON block from free-form text output.
    /// </summary>
    Task<StructuredOutputValidationResult> ExtractAndValidateAsync(
        string freeFormOutput,
        StructuredOutputSchema schema,
        CancellationToken ct = default);
}
