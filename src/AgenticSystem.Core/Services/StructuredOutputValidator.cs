using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using NJsonSchema;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Validates agent outputs against structured schemas using NJsonSchema (JSON Schema Draft 7+).
/// Extracts JSON from free-form text, validates required fields, and provides detailed errors.
/// </summary>
public partial class StructuredOutputValidator : IStructuredOutputValidator
{
    private readonly ILogger<StructuredOutputValidator> _logger;

    public StructuredOutputValidator(ILogger<StructuredOutputValidator> logger)
    {
        _logger = logger;
    }

    public async Task<StructuredOutputValidationResult> ValidateAsync(
        string rawOutput,
        StructuredOutputSchema schema,
        CancellationToken ct = default)
    {
        var errors = new List<string>();

        // Step 1: Try to parse as JSON
        JsonDocument? doc;
        try
        {
            doc = JsonDocument.Parse(rawOutput);
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return StructuredOutputValidationResult.Failure(rawOutput, errors);
        }

        // Step 2: Validate against JSON Schema via NJsonSchema
        if (!string.IsNullOrEmpty(schema.SchemaJson) && schema.SchemaJson != "{}")
        {
            try
            {
                var nJsonSchema = await JsonSchema.FromJsonAsync(schema.SchemaJson, ct);
                var validationErrors = nJsonSchema.Validate(doc.RootElement.GetRawText());
                
                foreach (var validationError in validationErrors)
                {
                    errors.Add($"Schema Validation Error at '{validationError.Path}': {validationError.Kind} - {validationError.ToString()}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse or validate schema JSON via NJsonSchema");
                errors.Add($"Internal Schema Validation Error: {ex.Message}");
            }
        }
        else
        {
            // Fallback Step 2: Validate required fields manually if no full schema is provided
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Expected JSON object, got {root.ValueKind}.");
                return StructuredOutputValidationResult.Failure(rawOutput, errors);
            }

            foreach (var field in schema.RequiredFields)
            {
                if (!root.TryGetProperty(field, out var prop))
                {
                    errors.Add($"Missing required field: '{field}'.");
                }
                else if (prop.ValueKind == JsonValueKind.Null)
                {
                    errors.Add($"Required field '{field}' is null.");
                }
                else if (prop.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(prop.GetString()))
                {
                    errors.Add($"Required field '{field}' is empty.");
                }
            }
        }

        if (errors.Count > 0)
        {
            return StructuredOutputValidationResult.Failure(rawOutput, errors);
        }

        return StructuredOutputValidationResult.Success(rawOutput, doc);
    }

    public async Task<StructuredOutputValidationResult> ExtractAndValidateAsync(
        string freeFormOutput,
        StructuredOutputSchema schema,
        CancellationToken ct = default)
    {
        // Try to extract JSON from markdown code blocks or raw JSON
        var jsonBlock = ExtractJsonBlock(freeFormOutput);

        if (jsonBlock == null)
        {
            return StructuredOutputValidationResult.Failure(
                freeFormOutput,
                ["No JSON block found in output. Expected ```json ... ``` or raw JSON object."]);
        }

        return await ValidateAsync(jsonBlock, schema, ct);
    }

    private static string? ExtractJsonBlock(string text)
    {
        // Try ```json ... ``` blocks first
        var codeBlockMatch = JsonCodeBlockRegex().Match(text);
        if (codeBlockMatch.Success)
        {
            return codeBlockMatch.Groups[1].Value.Trim();
        }

        // Try raw JSON object { ... }
        var trimmed = text.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        // Try to find first { ... } in text
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return text[firstBrace..(lastBrace + 1)];
        }

        return null;
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?([\s\S]*?)\n?```", RegexOptions.Compiled)]
    private static partial Regex JsonCodeBlockRegex();
}
