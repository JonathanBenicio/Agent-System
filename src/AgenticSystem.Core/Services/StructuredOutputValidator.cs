using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Validates agent outputs against structured schemas.
/// Extracts JSON from free-form text, validates required fields, and provides detailed errors.
/// </summary>
public partial class StructuredOutputValidator : IStructuredOutputValidator
{
    private readonly ILogger<StructuredOutputValidator> _logger;

    public StructuredOutputValidator(ILogger<StructuredOutputValidator> logger)
    {
        _logger = logger;
    }

    public Task<StructuredOutputValidationResult> ValidateAsync(
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
            return Task.FromResult(StructuredOutputValidationResult.Failure(rawOutput, errors));
        }

        // Step 2: Validate required fields
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"Expected JSON object, got {root.ValueKind}.");
            return Task.FromResult(StructuredOutputValidationResult.Failure(rawOutput, errors));
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

        // Step 3: Validate against JSON Schema (basic type checking)
        if (!string.IsNullOrEmpty(schema.SchemaJson) && schema.SchemaJson != "{}")
        {
            try
            {
                var schemaDoc = JsonDocument.Parse(schema.SchemaJson);
                var schemaRoot = schemaDoc.RootElement;

                if (schemaRoot.TryGetProperty("properties", out var properties))
                {
                    foreach (var prop in properties.EnumerateObject())
                    {
                        if (root.TryGetProperty(prop.Name, out var valueProp) &&
                            prop.Value.TryGetProperty("type", out var expectedType))
                        {
                            var expected = expectedType.GetString();
                            var actual = valueProp.ValueKind;

                            var typeMismatch = expected switch
                            {
                                "string" => actual != JsonValueKind.String,
                                "number" or "integer" => actual != JsonValueKind.Number,
                                "boolean" => actual is not JsonValueKind.True and not JsonValueKind.False,
                                "array" => actual != JsonValueKind.Array,
                                "object" => actual != JsonValueKind.Object,
                                _ => false
                            };

                            if (typeMismatch)
                            {
                                errors.Add($"Field '{prop.Name}' expected type '{expected}', got '{actual}'.");
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse schema JSON for validation");
            }
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(StructuredOutputValidationResult.Failure(rawOutput, errors));
        }

        return Task.FromResult(StructuredOutputValidationResult.Success(rawOutput, doc));
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
