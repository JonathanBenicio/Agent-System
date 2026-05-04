using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML20 — Busca MCPs e plugins quando tools requeridas não estão registradas.
/// Pesquisa em catálogos conhecidos e retorna sugestões sem auto-instalar.
/// </summary>
public class ToolDiscoveryService : IToolDiscoveryService
{
    private readonly ILogger<ToolDiscoveryService> _logger;

    // Catálogo interno de tools conhecidas → pacotes MCP
    private static readonly Dictionary<string, ToolSuggestion> KnownRegistry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["finance-api"] = new ToolSuggestion
        {
            ToolName = "finance-api",
            PackageName = "@modelcontextprotocol/server-finance",
            Source = "npm",
            Description = "MCP server for financial data and portfolio management",
            InstallCommand = "npx @modelcontextprotocol/server-finance",
            RelevanceScore = 0.95
        },
        ["calendar"] = new ToolSuggestion
        {
            ToolName = "calendar",
            PackageName = "@modelcontextprotocol/server-google-calendar",
            Source = "npm",
            Description = "MCP server for Google Calendar integration",
            InstallCommand = "npx @modelcontextprotocol/server-google-calendar",
            RelevanceScore = 0.90
        },
        ["email"] = new ToolSuggestion
        {
            ToolName = "email",
            PackageName = "@modelcontextprotocol/server-gmail",
            Source = "npm",
            Description = "MCP server for Gmail integration",
            InstallCommand = "npx @modelcontextprotocol/server-gmail",
            RelevanceScore = 0.90
        },
        ["database"] = new ToolSuggestion
        {
            ToolName = "database",
            PackageName = "@modelcontextprotocol/server-postgres",
            Source = "npm",
            Description = "MCP server for PostgreSQL database access",
            InstallCommand = "npx @modelcontextprotocol/server-postgres",
            RelevanceScore = 0.85
        },
        ["github"] = new ToolSuggestion
        {
            ToolName = "github",
            PackageName = "@modelcontextprotocol/server-github",
            Source = "npm",
            Description = "MCP server for GitHub API integration",
            InstallCommand = "npx @modelcontextprotocol/server-github",
            RelevanceScore = 0.95
        },
        ["filesystem"] = new ToolSuggestion
        {
            ToolName = "filesystem",
            PackageName = "@modelcontextprotocol/server-filesystem",
            Source = "npm",
            Description = "MCP server for local filesystem operations",
            InstallCommand = "npx @modelcontextprotocol/server-filesystem /path",
            RelevanceScore = 0.90
        },
        ["search"] = new ToolSuggestion
        {
            ToolName = "search",
            PackageName = "@modelcontextprotocol/server-brave-search",
            Source = "npm",
            Description = "MCP server for Brave Search web queries",
            InstallCommand = "npx @modelcontextprotocol/server-brave-search",
            RelevanceScore = 0.85
        },
        ["jira"] = new ToolSuggestion
        {
            ToolName = "jira",
            PackageName = "@anthropic/mcp-server-atlassian",
            Source = "npm",
            Description = "MCP server for Jira/Confluence integration",
            InstallCommand = "npx @anthropic/mcp-server-atlassian",
            RelevanceScore = 0.90
        },
        ["slack"] = new ToolSuggestion
        {
            ToolName = "slack",
            PackageName = "@modelcontextprotocol/server-slack",
            Source = "npm",
            Description = "MCP server for Slack messaging",
            InstallCommand = "npx @modelcontextprotocol/server-slack",
            RelevanceScore = 0.85
        }
    };

    public ToolDiscoveryService(ILogger<ToolDiscoveryService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<ToolSuggestion>> SearchAsync(IReadOnlyList<string> missingTools, CancellationToken ct = default)
    {
        var suggestions = new List<ToolSuggestion>();

        foreach (var toolId in missingTools)
        {
            ct.ThrowIfCancellationRequested();

            if (KnownRegistry.TryGetValue(toolId, out var knownSuggestion))
            {
                suggestions.Add(knownSuggestion);
                _logger.LogDebug("📦 Match no catálogo interno: {Tool} → {Package}", toolId, knownSuggestion.PackageName);
            }
            else
            {
                // Fuzzy match por substring
                var fuzzy = KnownRegistry.Values
                    .Where(s => s.ToolName.Contains(toolId, StringComparison.OrdinalIgnoreCase)
                             || s.Description.Contains(toolId, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s with { RelevanceScore = s.RelevanceScore * 0.7 })
                    .ToList();

                if (fuzzy.Count > 0)
                {
                    suggestions.AddRange(fuzzy);
                }
                else
                {
                    // Sugestão genérica de busca
                    suggestions.Add(new ToolSuggestion
                    {
                        ToolName = toolId,
                        PackageName = $"(não encontrado)",
                        Source = "manual",
                        Description = $"Busque manualmente: npm search mcp-server-{toolId} ou GitHub Topics 'mcp-server'",
                        InstallCommand = $"npm search @modelcontextprotocol/server-{toolId}",
                        RelevanceScore = 0.2
                    });
                    _logger.LogDebug("❓ Sem match para: {Tool} — sugestão genérica", toolId);
                }
            }
        }

        return await Task.FromResult<IReadOnlyList<ToolSuggestion>>(
            suggestions.OrderByDescending(s => s.RelevanceScore).ToList());
    }
}
