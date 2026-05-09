using System.Text.RegularExpressions;

namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Prompt Management — Templates & Dynamic Variables
// ═══════════════════════════════════════════════════════════

/// <summary>
/// A managed prompt template with dynamic variable substitution.
/// Supports versioning and locale-specific variants.
/// </summary>
public class PromptTemplate
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string TemplateBody { get; init; } = string.Empty;
    public int Version { get; set; } = 1;
    public string Locale { get; init; } = "pt-BR";
    public List<string> Variables { get; init; } = [];
    public string? Description { get; init; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; init; }

    /// <summary>
    /// Renders the template by substituting {{variable}} placeholders with values.
    /// </summary>
    public string Render(Dictionary<string, string> variables)
    {
        var result = TemplateBody;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    /// <summary>
    /// Extracts all {{variable}} placeholders from the template body.
    /// </summary>
    public static List<string> ExtractVariables(string templateBody)
    {
        var matches = Regex.Matches(templateBody, @"\{\{(\w+)\}\}");
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }
}
