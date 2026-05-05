using System.Text;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Skills;

internal sealed class DynamicSkillCatalogHostedService : IHostedService
{
    private readonly ISkillManager _skillManager;
    private readonly DynamicSkillsOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DynamicSkillCatalogHostedService> _logger;

    public DynamicSkillCatalogHostedService(
        ISkillManager skillManager,
        IOptions<DynamicSkillsOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<DynamicSkillCatalogHostedService> logger)
    {
        _skillManager = skillManager;
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Dynamic skills loader is disabled.");
            return Task.CompletedTask;
        }

        var directory = ResolveSkillsDirectory();
        if (directory is null)
        {
            _logger.LogDebug("No dynamic skills directory found for configured path {Directory}.", _options.Directory);
            return Task.CompletedTask;
        }

        var files = Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var skill = LoadSkill(file);
                if (skill is null)
                {
                    continue;
                }

                if (_options.OverrideExistingSkills)
                {
                    _skillManager.UnregisterSkill(skill.Id);
                }

                _skillManager.RegisterSkill(skill);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load dynamic skill from {FilePath}", file);
            }
        }

        _logger.LogInformation("📚 Dynamic skill loader processed {Count} file(s) from {Directory}", files.Count, directory);
        return Task.CompletedTask;
    }

    private string? ResolveSkillsDirectory()
    {
        if (Path.IsPathRooted(_options.Directory))
        {
            return Directory.Exists(_options.Directory) ? _options.Directory : null;
        }

        var candidates = new[]
        {
            Path.Combine(_hostEnvironment.ContentRootPath, _options.Directory),
            Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, "..", "..", _options.Directory))
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private DeclarativeSkill? LoadSkill(string filePath)
    {
        var definition = filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? ParseJsonSkill(filePath)
            : ParseYamlSkill(filePath);

        if (definition is null || !definition.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new InvalidOperationException($"Dynamic skill file '{filePath}' must define non-empty id and name.");
        }

        return new DeclarativeSkill(
            definition.Id,
            definition.Name,
            string.IsNullOrWhiteSpace(definition.Domain) ? "general" : definition.Domain,
            ParseSkillType(definition.Type),
            new SkillContent
            {
                SystemPromptFragment = definition.SystemPromptFragment ?? string.Empty,
                FewShotExamples = definition.FewShotExamples,
                Metadata = definition.Metadata?.Count > 0
                    ? new Dictionary<string, string>(definition.Metadata, StringComparer.OrdinalIgnoreCase)
                    : null
            });
    }

    private static DeclarativeSkillDefinition? ParseJsonSkill(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<DeclarativeSkillDefinition>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static DeclarativeSkillDefinition ParseYamlSkill(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var definition = new DeclarativeSkillDefinition();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? currentBlockKey = null;
        var currentBlockIndent = 0;
        var currentBlock = new StringBuilder();
        var inMetadata = false;
        var metadataIndent = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                if (currentBlockKey is not null)
                {
                    currentBlock.AppendLine();
                }

                continue;
            }

            var indent = line.Length - line.TrimStart().Length;

            if (currentBlockKey is not null)
            {
                if (indent > currentBlockIndent)
                {
                    currentBlock.AppendLine(line[(currentBlockIndent + 2)..].TrimEnd());
                    continue;
                }

                AssignScalar(definition, currentBlockKey, currentBlock.ToString().TrimEnd());
                currentBlockKey = null;
                currentBlock.Clear();
            }

            if (inMetadata)
            {
                if (indent > metadataIndent && TryParseKeyValue(trimmed, out var metadataKey, out var metadataValue))
                {
                    metadata[metadataKey] = Unquote(metadataValue);
                    continue;
                }

                inMetadata = false;
            }

            if (!TryParseKeyValue(trimmed, out var key, out var value))
            {
                continue;
            }

            if (string.Equals(key, "metadata", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(value))
            {
                inMetadata = true;
                metadataIndent = indent;
                continue;
            }

            if (value is "|" or ">")
            {
                currentBlockKey = key;
                currentBlockIndent = indent;
                currentBlock.Clear();
                continue;
            }

            AssignScalar(definition, key, Unquote(value));
        }

        if (currentBlockKey is not null)
        {
            AssignScalar(definition, currentBlockKey, currentBlock.ToString().TrimEnd());
        }

        definition.Metadata = metadata.Count > 0 ? metadata : null;
        return definition;
    }

    private static void AssignScalar(DeclarativeSkillDefinition definition, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "id":
                definition.Id = value;
                break;
            case "name":
                definition.Name = value;
                break;
            case "domain":
                definition.Domain = value;
                break;
            case "type":
                definition.Type = value;
                break;
            case "enabled":
                definition.Enabled = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                break;
            case "systempromptfragment":
            case "system_prompt_fragment":
            case "prompt":
                definition.SystemPromptFragment = value;
                break;
            case "fewshotexamples":
            case "few_shot_examples":
                definition.FewShotExamples = value;
                break;
        }
    }

    private static SkillType ParseSkillType(string? type)
    {
        return Enum.TryParse<SkillType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : SkillType.Instruction;
    }

    private static bool TryParseKeyValue(string input, out string key, out string value)
    {
        var separatorIndex = input.IndexOf(':');
        if (separatorIndex <= 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = input[..separatorIndex].Trim();
        value = input[(separatorIndex + 1)..].Trim();
        return true;
    }

    private static string Unquote(string value)
    {
        return value.Trim().Trim('"').Trim('\'');
    }

    private sealed class DeclarativeSkillDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Domain { get; set; } = "general";
        public string Type { get; set; } = nameof(SkillType.Instruction);
        public bool Enabled { get; set; } = true;
        public string? SystemPromptFragment { get; set; }
        public string? FewShotExamples { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }
}