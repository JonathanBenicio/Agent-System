using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Text;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Sync;

/// <summary>
/// Persistência de eventos e agents em arquivos Markdown (formato Obsidian).
/// Implementação file-based para uso local sem dependência de Obsidian.
/// </summary>
public class FileObsidianSync : IObsidianSync
{
    private readonly string _vaultPath;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<FileObsidianSync> _logger;

    public FileObsidianSync(
        IVectorStore vectorStore,
        ILogger<FileObsidianSync> logger,
        string? vaultPath = null)
    {
        _vectorStore = vectorStore;
        _logger = logger;
        _vaultPath = vaultPath ?? Path.Combine(AppContext.BaseDirectory, "vault");
        EnsureDirectories();
    }

    public async Task SaveSessionEventAsync(AgentEvent agentEvent)
    {
        var dir = Path.Combine(_vaultPath, "sessions", agentEvent.SessionId);
        Directory.CreateDirectory(dir);

        var fileName = $"{agentEvent.Timestamp:yyyyMMdd-HHmmss}_{agentEvent.AgentName}.md";
        var filePath = Path.Combine(dir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"id: {agentEvent.Id}");
        sb.AppendLine($"session: {agentEvent.SessionId}");
        sb.AppendLine($"agent: {agentEvent.AgentName}");
        sb.AppendLine($"tier: {agentEvent.AgentTier}");
        sb.AppendLine($"timestamp: {agentEvent.Timestamp:O}");
        if (agentEvent.Tags.Count > 0)
            sb.AppendLine($"tags: [{string.Join(", ", agentEvent.Tags)}]");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# Session Event — {agentEvent.AgentName}");
        sb.AppendLine();
        sb.AppendLine("## Input");
        sb.AppendLine($"```\n{agentEvent.UserInput}\n```");
        sb.AppendLine();
        sb.AppendLine("## Response");
        sb.AppendLine(agentEvent.AgentResponse);
        sb.AppendLine();

        if (agentEvent.ActionsPerformed.Count > 0)
        {
            sb.AppendLine("## Actions");
            foreach (var action in agentEvent.ActionsPerformed)
                sb.AppendLine($"- {action}");
            sb.AppendLine();
        }

        if (agentEvent.ToolsUsed.Count > 0)
        {
            sb.AppendLine("## Tools Used");
            foreach (var tool in agentEvent.ToolsUsed)
                sb.AppendLine($"- {tool}");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());

        // Index in vector store
        var doc = new EmbeddingDocument
        {
            Id = agentEvent.Id,
            Content = $"{agentEvent.UserInput}\n\n{agentEvent.AgentResponse}",
            Type = "session_event",
            Collection = "sessions",
            Metadata = new Dictionary<string, string>
            {
                ["agent"] = agentEvent.AgentName,
                ["session"] = agentEvent.SessionId,
                ["tier"] = agentEvent.AgentTier.ToString()
            }
        };
        await _vectorStore.UpsertAsync(doc);

        _logger.LogDebug("📝 Session event saved: {FilePath}", filePath);
    }

    public async Task SaveAgentDefinitionAsync(IAgent agent)
    {
        var dir = Path.Combine(_vaultPath, "agents");
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, $"{agent.Name}.md");

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {agent.Name}");
        sb.AppendLine($"tier: {agent.Tier}");
        sb.AppendLine($"created: {agent.CreatedAt:O}");
        sb.AppendLine($"active: {agent.IsActive}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# Agent: {agent.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Description**: {agent.Description}");
        sb.AppendLine($"**Tier**: {agent.Tier}");
        sb.AppendLine($"**Active**: {agent.IsActive}");
        sb.AppendLine();

        if (agent.AvailableTools.Any())
        {
            sb.AppendLine("## Available Tools");
            foreach (var tool in agent.AvailableTools)
                sb.AppendLine($"- {tool}");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());

        // Index in vector store
        var doc = new EmbeddingDocument
        {
            Id = $"agent-{agent.Name}",
            Content = $"{agent.Name}: {agent.Description}",
            Type = "agent",
            Collection = "agents",
            Metadata = new Dictionary<string, string>
            {
                ["name"] = agent.Name,
                ["tier"] = agent.Tier.ToString()
            }
        };
        await _vectorStore.UpsertAsync(doc);

        _logger.LogDebug("📝 Agent definition saved: {FilePath}", filePath);
    }

    public async Task<List<ObsidianNote>> GetRelevantNotesAsync(string query)
    {
        var result = await _vectorStore.SearchAsync(query, SearchScope.All, 5);

        return result.Matches.Select(m => new ObsidianNote
        {
            Id = m.Id,
            Title = m.Metadata.GetValueOrDefault("title", m.Id),
            Content = m.Content,
            Tags = m.Type != null ? new List<string> { m.Type } : new List<string>()
        }).ToList();
    }

    public async Task StartFileWatcherAsync()
    {
        _logger.LogInformation("👁️ File watcher started on vault: {VaultPath}", _vaultPath);
        // In-process watcher — indexes new/changed .md files
        if (!Directory.Exists(_vaultPath)) return;

        var watcher = new FileSystemWatcher(_vaultPath, "*.md")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        watcher.Changed += async (_, e) => await IndexFileAsync(e.FullPath);
        watcher.Created += async (_, e) => await IndexFileAsync(e.FullPath);

        await Task.CompletedTask;
    }

    public async Task IndexExistingVaultAsync()
    {
        if (!Directory.Exists(_vaultPath))
        {
            _logger.LogWarning("📁 Vault path does not exist: {VaultPath}", _vaultPath);
            return;
        }

        var files = Directory.GetFiles(_vaultPath, "*.md", SearchOption.AllDirectories);
        _logger.LogInformation("📁 Indexing {Count} files from vault", files.Length);

        foreach (var file in files)
        {
            await IndexFileAsync(file);
        }
    }

    private async Task IndexFileAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var relativePath = Path.GetRelativePath(_vaultPath, filePath);
            var collection = Path.GetDirectoryName(relativePath)?.Split(Path.DirectorySeparatorChar).FirstOrDefault() ?? "notes";

            var doc = new EmbeddingDocument
            {
                Id = relativePath,
                Content = content,
                Type = "note",
                Collection = collection,
                Metadata = new Dictionary<string, string>
                {
                    ["title"] = Path.GetFileNameWithoutExtension(filePath),
                    ["file_path"] = filePath
                }
            };

            await _vectorStore.UpsertAsync(doc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index file: {FilePath}", filePath);
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_vaultPath, "sessions"));
        Directory.CreateDirectory(Path.Combine(_vaultPath, "agents"));
        Directory.CreateDirectory(Path.Combine(_vaultPath, "notes"));
    }
}
