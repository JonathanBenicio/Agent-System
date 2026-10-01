using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Provedor de Skills baseado em arquivos físicos Markdown (.md).
/// Realiza a varredura dinâmica de diretórios carregando definições de habilidades com suporte a Frontmatter YAML.
/// </summary>
public class AgentFileSkillsSource
{
    private readonly string _skillsDirectory;
    private readonly ILogger<AgentFileSkillsSource> _logger;

    public AgentFileSkillsSource(string? skillsDirectory, ILogger<AgentFileSkillsSource> logger)
    {
        _skillsDirectory = skillsDirectory ?? Path.Combine(AppContext.BaseDirectory, "skills");
        _logger = logger;
        EnsureDirectoryAndDefaults();
    }

    /// <summary>
    /// Varre o diretório e carrega todas as skills Markdown parseadas.
    /// </summary>
    public async Task<IEnumerable<ISkill>> LoadSkillsAsync()
    {
        var skills = new List<ISkill>();
        if (!Directory.Exists(_skillsDirectory))
        {
            return skills;
        }

        var files = Directory.GetFiles(_skillsDirectory, "*.md");
        foreach (var file in files)
        {
            try
            {
                var skill = await ParseSkillFileAsync(file);
                if (skill != null)
                {
                    skills.Add(skill);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse skill markdown file: {FilePath}", file);
            }
        }

        _logger.LogInformation("📚 Loaded {Count} markdown skills from {Directory}", skills.Count, _skillsDirectory);
        return skills;
    }

    private void EnsureDirectoryAndDefaults()
    {
        if (!Directory.Exists(_skillsDirectory))
        {
            Directory.CreateDirectory(_skillsDirectory);
        }

        // Criar arquivos de skill padrão se estiver vazio para autoinicialização amigável
        var files = Directory.GetFiles(_skillsDirectory, "*.md");
        if (files.Length == 0)
        {
            CreateDefaultSkillFile("coding-assistant", "Coding Assistant", "work", "Instruction", 
                "You are an expert software engineer.\n- Follow Clean Code principles.\n- Suggest tests alongside implementation.",
                new Dictionary<string, string> { ["languages"] = "csharp,typescript,python", ["frameworks"] = "dotnet,react" });
            
            CreateDefaultSkillFile("productivity", "Productivity & Planning", "personal", "Instruction", 
                "You are a productivity coach.\n- Break tasks into actionable steps.\n- Use timeboxing.",
                new Dictionary<string, string> { ["methods"] = "pomodoro,eisenhower" });
        }
    }

    private void CreateDefaultSkillFile(string id, string name, string domain, string type, string prompt, Dictionary<string, string> metadata)
    {
        var filePath = Path.Combine(_skillsDirectory, $"{id}.md");
        var metadataJson = JsonSerializer.Serialize(metadata);
        var content = $@"---
id: {id}
name: {name}
domain: {domain}
type: {type}
metadata: {metadataJson}
---

{prompt}";

        File.WriteAllText(filePath, content);
        _logger.LogInformation("📚 Seeded default skill markdown: {FilePath}", filePath);
    }

    private async Task<ISkill?> ParseSkillFileAsync(string filePath)
    {
        var fileContent = await File.ReadAllTextAsync(filePath);
        
        // Regex para extrair Frontmatter delimitado por ---
        var match = Regex.Match(fileContent, @"^---\r?\n(.*?)\r?\n---\r?\n(.*)$", RegexOptions.Singleline);
        if (!match.Success)
        {
            _logger.LogWarning("Markdown file does not have valid frontmatter: {FilePath}", filePath);
            return null;
        }

        var frontmatter = match.Groups[1].Value;
        var promptBody = match.Groups[2].Value.Trim();

        // Parser Frontmatter simples em YAML/JSON-like
        var id = ExtractValue(frontmatter, "id") ?? Path.GetFileNameWithoutExtension(filePath);
        var name = ExtractValue(frontmatter, "name") ?? id;
        var domain = ExtractValue(frontmatter, "domain") ?? "general";
        var typeStr = ExtractValue(frontmatter, "type") ?? "Instruction";
        
        var skillType = Enum.TryParse<SkillType>(typeStr, true, out var parsedType) 
            ? parsedType 
            : SkillType.Instruction;

        var metadata = new Dictionary<string, string>();
        var metadataMatch = Regex.Match(frontmatter, @"metadata:\s*({.*?})", RegexOptions.Singleline);
        if (metadataMatch.Success)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataMatch.Groups[1].Value);
                if (dict != null)
                {
                    metadata = dict;
                }
            }
            catch
            {
                // Fallback para strings normais se falhar no JSON
            }
        }

        return new FileBasedSkill(id, name, domain, skillType, promptBody, metadata);
    }

    private static string? ExtractValue(string frontmatter, string key)
    {
        var match = Regex.Match(frontmatter, $@"{key}:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim().Trim('"', '\'') : null;
    }
}

/// <summary>
/// Implementação em runtime de ISkill para carregamento a partir de arquivos físicos.
/// </summary>
public class FileBasedSkill : ISkill
{
    public string Id { get; }
    public string Name { get; }
    public string Domain { get; }
    public SkillType Type { get; }
    private readonly string _systemPromptFragment;
    private readonly Dictionary<string, string> _metadata;

    public FileBasedSkill(string id, string name, string domain, SkillType type, string systemPromptFragment, Dictionary<string, string> metadata)
    {
        Id = id;
        Name = name;
        Domain = domain;
        Type = type;
        _systemPromptFragment = systemPromptFragment;
        _metadata = metadata;
    }

    public Task<SkillContent> GetContentAsync(SkillContext context)
    {
        return Task.FromResult(new SkillContent
        {
            SystemPromptFragment = _systemPromptFragment,
            Metadata = _metadata
        });
    }
}
