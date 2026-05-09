using System;

namespace AgenticSystem.Core.Attributes;

/// <summary>
/// Marks a class or method to enforce a structured output schema via JSON Schema Draft 7.
/// When applied, the framework will automatically validate the LLM's output against the schema
/// and perform an auto-retry if validation fails.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AgenticJsonSchemaAttribute : Attribute
{
    /// <summary>
    /// If true, the system will automatically retry on validation failure.
    /// Default is true.
    /// </summary>
    public bool AutoRetryOnFailure { get; set; } = true;

    /// <summary>
    /// Maximum number of retries for auto-fix before throwing an exception.
    /// Default is 2.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// An optional custom JSON Schema. If not provided, the framework will 
    /// attempt to generate one automatically using NJsonSchema from the target type.
    /// </summary>
    public string? CustomSchemaJson { get; set; }
}
