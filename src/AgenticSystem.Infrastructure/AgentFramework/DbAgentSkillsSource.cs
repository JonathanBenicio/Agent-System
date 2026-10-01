using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Provedor de Skills integrado de forma nativa e exclusiva ao PostgreSQL.
/// Substitui o escaneamento de arquivos em disco por uma tabela no banco operacional com auto-seeding dinâmico de habilidades padrão por Tenant.
/// </summary>
public class DbAgentSkillsSource
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<DbAgentSkillsSource> _logger;
    private readonly ISystemOperationContextAccessor _systemOperations;

    public DbAgentSkillsSource(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<DbAgentSkillsSource> logger,
        ISystemOperationContextAccessor systemOperations)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _systemOperations = systemOperations ?? throw new ArgumentNullException(nameof(systemOperations));
    }

    /// <summary>
    /// Busca todas as skills cadastradas no PostgreSQL para o Tenant ativo.
    /// Se o Tenant não possuir skills ainda, realiza o Auto-Seeding das skills nativas do sistema.
    /// </summary>
    public async Task<IEnumerable<ISkill>> LoadSkillsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            
            // 1. Consulta todas as habilidades cadastradas no banco de dados para o Tenant atual.
            // Os filtros globais de ITenantEntity segregarão os registros dinamicamente.
            var entities = await db.AgentSkills.AsNoTracking().ToListAsync(ct);

            // 2. Se a lista de skills do Tenant estiver vazia, dispara a rotina automática de Auto-Seeding
            entities = await SeedDefaultSkillsAsync(db, entities, ct);

            var platformSkills = await LoadPlatformSkillsAsync(db, ct);
            var tenantSystemKeys = entities
                .Where(skill => skill.IsSystem)
                .Select(skill => BuildSkillKey(skill.Name, skill.Domain))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var merged = platformSkills
                .Where(skill => skill.IsEnabled && !tenantSystemKeys.Contains(BuildSkillKey(skill.Name, skill.Domain)))
                .Select(MapPlatformSkill)
                .Concat(entities.Where(skill => skill.IsEnabled).Select(MapTenantSkill));

            return merged.ToList();
    }

    private async Task<List<PlatformAgentSkillEntity>> LoadPlatformSkillsAsync(AgenticDbContext db, CancellationToken ct)
    {
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformCatalogRead);
        _systemOperations.Require(SystemOperationKind.PlatformCatalogRead);
        return await db.PlatformAgentSkills.AsNoTracking().ToListAsync(ct);
    }

    private static string BuildSkillKey(string name, string domain) => $"{domain.Trim()}\0{name.Trim()}";

    private static ISkill MapTenantSkill(DbSkillEntity skill) => new DbBasedSkill(
        skill.Id,
        skill.Name,
        skill.Domain,
        Enum.TryParse<SkillType>(skill.Type, true, out var parsedType) ? parsedType : SkillType.Instruction,
        skill.SystemPromptFragment,
        skill.FewShotExamples,
        skill.IsSystem,
        DeserializeMetadata(skill.MetadataJson));

    private static ISkill MapPlatformSkill(PlatformAgentSkillEntity skill) => new DbBasedSkill(
        skill.Id,
        skill.Name,
        skill.Domain,
        Enum.TryParse<SkillType>(skill.Type, true, out var parsedType) ? parsedType : SkillType.Instruction,
        skill.SystemPromptFragment,
        skill.FewShotExamples,
        skill.IsSystem,
        DeserializeMetadata(skill.MetadataJson));

    private static Dictionary<string, string> DeserializeMetadata(string? json) =>
        string.IsNullOrEmpty(json)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    /// <summary>
    /// Provisiona automaticamente o catálogo de skills nativas do sistema para o Tenant atual no banco operacional.
    /// </summary>
    private async Task<List<DbSkillEntity>> SeedDefaultSkillsAsync(AgenticDbContext db, List<DbSkillEntity> existing, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(db.CurrentTenantId))
            throw new InvalidOperationException("Cannot seed default skills without an active tenant.");

        _logger.LogWarning("📚 Tenant '{TenantId}' sem catálogo de habilidades. Semeando skills padrões no banco...", db.CurrentTenantId);
        var tenantSuffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(db.CurrentTenantId)))[..16].ToLowerInvariant();

        var defaultSkills = new List<DbSkillEntity>
        {
            new()
            {
                Id = $"coding-assistant-{tenantSuffix}",
                TenantId = db.CurrentTenantId,
                Name = "Coding Assistant",
                Domain = "work",
                Type = "Instruction",
                SystemPromptFragment = "You are an expert software engineer.\n- Follow Clean Code principles.\n- Suggest tests alongside implementation.",
                IsSystem = true,
                FewShotExamples = "User: How to sum two numbers?\nAgent: ```csharp\npublic int Sum(int a, int b) => a + b;\n```",
                MetadataJson = "{\"category\":\"development\",\"author\":\"system\"}"
            },
            new()
            {
                Id = $"productivity-{tenantSuffix}",
                TenantId = db.CurrentTenantId,
                Name = "Productivity & Planning",
                Domain = "personal",
                Type = "Instruction",
                SystemPromptFragment = "You are a productivity coach.\n- Break tasks into actionable steps.\n- Use timeboxing and Pomodoro techniques.",
                IsSystem = true,
                MetadataJson = "{\"category\":\"organization\",\"author\":\"system\"}"
            },
            new()
            {
                Id = $"creative-writing-{tenantSuffix}",
                TenantId = db.CurrentTenantId,
                Name = "Creative Writing Specialist",
                Domain = "general",
                Type = "Instruction",
                SystemPromptFragment = "You are a creative writer and copyeditor.\n- Help refine tone and style.\n- Make content engaging.",
                IsSystem = true,
                MetadataJson = "{\"category\":\"writing\",\"author\":\"system\"}"
            },
            new()
            {
                Id = $"data-analysis-{tenantSuffix}",
                TenantId = db.CurrentTenantId,
                Name = "Data Analysis Helper",
                Domain = "work",
                Type = "Instruction",
                SystemPromptFragment = "You are a data analyst assistant.\n- Break down datasets logically.\n- Formulate SQL queries.",
                IsSystem = true,
                MetadataJson = "{\"category\":\"analytics\",\"author\":\"system\"}"
            }
        };

        var knownIds = existing.Select(skill => skill.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownNames = existing.Where(skill => skill.IsSystem).Select(skill => skill.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = defaultSkills.Where(skill => !knownIds.Contains(skill.Id) && !knownNames.Contains(skill.Name)).ToList();
        if (missing.Count == 0)
            return existing;

        try
        {
            db.AgentSkills.AddRange(missing);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("📚 Auto-Seeding de skills padrão concluído para o Tenant '{TenantId}' ({Count} skills provisionadas).", db.CurrentTenantId, missing.Count);
            return existing.Concat(missing).ToList();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _logger.LogDebug(ex, "Concurrent default skill seeding detected for tenant {TenantId}; reloading after unique-key conflict.", db.CurrentTenantId);
            db.ChangeTracker.Clear();
            return await db.AgentSkills.AsNoTracking().ToListAsync(ct);
        }
    }
}

/// <summary>
/// Implementação em runtime de ISkill para carregamento a partir de registros físicos do PostgreSQL.
/// </summary>
public class DbBasedSkill : ISkill
{
    public string Id { get; }
    public string Name { get; }
    public string Domain { get; }
    public SkillType Type { get; }
    public bool IsSystem { get; }
    
    private readonly string _systemPromptFragment;
    private readonly string? _fewShotExamples;
    private readonly Dictionary<string, string> _metadata;

    public DbBasedSkill(
        string id, 
        string name, 
        string domain, 
        SkillType type, 
        string systemPromptFragment, 
        string? fewShotExamples,
        bool isSystem,
        Dictionary<string, string> metadata)
    {
        Id = id;
        Name = name;
        Domain = domain;
        Type = type;
        IsSystem = isSystem;
        _systemPromptFragment = systemPromptFragment;
        _fewShotExamples = fewShotExamples;
        _metadata = metadata;
    }

    public Task<SkillContent> GetContentAsync(SkillContext context)
    {
        return Task.FromResult(new SkillContent
        {
            SystemPromptFragment = _systemPromptFragment,
            FewShotExamples = _fewShotExamples,
            Metadata = _metadata
        });
    }
}
