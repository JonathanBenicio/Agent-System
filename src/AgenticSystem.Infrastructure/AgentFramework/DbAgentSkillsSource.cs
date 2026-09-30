using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
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

    public DbAgentSkillsSource(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<DbAgentSkillsSource> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Busca todas as skills cadastradas no PostgreSQL para o Tenant ativo.
    /// Se o Tenant não possuir skills ainda, realiza o Auto-Seeding das skills nativas do sistema.
    /// </summary>
    public async Task<IEnumerable<ISkill>> LoadSkillsAsync(CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            
            // 1. Consulta todas as habilidades cadastradas no banco de dados para o Tenant atual.
            // Os filtros globais de ITenantEntity segregarão os registros dinamicamente.
            var entities = await db.AgentSkills.AsNoTracking().ToListAsync(ct);

            // 2. Se a lista de skills do Tenant estiver vazia, dispara a rotina automática de Auto-Seeding
            if (entities.Count == 0)
            {
                entities = await SeedDefaultSkillsAsync(db, ct);
            }

            return entities.Select(e => new DbBasedSkill(
                e.Id,
                e.Name,
                e.Domain,
                Enum.TryParse<SkillType>(e.Type, true, out var parsedType) ? parsedType : SkillType.Instruction,
                e.SystemPromptFragment,
                e.FewShotExamples,
                e.IsSystem,
                string.IsNullOrEmpty(e.MetadataJson) 
                    ? new Dictionary<string, string>() 
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(e.MetadataJson) ?? new()
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database agent skills. Falling back to empty list.");
            return Enumerable.Empty<ISkill>();
        }
    }

    /// <summary>
    /// Provisiona automaticamente o catálogo de skills nativas do sistema para o Tenant atual no banco operacional.
    /// </summary>
    private async Task<List<DbSkillEntity>> SeedDefaultSkillsAsync(AgenticDbContext db, CancellationToken ct)
    {
        _logger.LogWarning("📚 Tenant '{TenantId}' sem catálogo de habilidades. Semeando skills padrões no banco...", db.CurrentTenantId);

        var defaultSkills = new List<DbSkillEntity>
        {
            new()
            {
                Id = "coding-assistant",
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
                Id = "productivity",
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
                Id = "creative-writing",
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
                Id = "data-analysis",
                TenantId = db.CurrentTenantId,
                Name = "Data Analysis Helper",
                Domain = "work",
                Type = "Instruction",
                SystemPromptFragment = "You are a data analyst assistant.\n- Break down datasets logically.\n- Formulate SQL queries.",
                IsSystem = true,
                MetadataJson = "{\"category\":\"analytics\",\"author\":\"system\"}"
            }
        };

        try
        {
            db.AgentSkills.AddRange(defaultSkills);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("📚 Auto-Seeding de skills padrão concluído com sucesso para o Tenant '{TenantId}' (4 skills provisionadas).", db.CurrentTenantId);
            return defaultSkills;
        }
        catch (DbUpdateException ex)
        {
            // Tratamento de concorrência se outra instância já inseriu simultaneamente
            _logger.LogWarning(ex, "Concorrência detectada: Outra instância já semeou as skills padrão para o Tenant '{TenantId}'.", db.CurrentTenantId);
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
