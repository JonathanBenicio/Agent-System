using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using AgenticSystem.Infrastructure.AgentFramework;

namespace AgenticSystem.Api.Controllers;

/// <summary>
/// API REST unificada para a gestão 100% dinâmica e orientada a Banco de Dados (PostgreSQL) das Habilidades (Skills) de Agentes.
/// Permite CRUD total, download de templates markdown de skills, e upload de arquivos com parsing regex do Frontmatter.
/// </summary>
[ApiController]
[Authorize]
[Route("api/agent/skills")]
public class AgentSkillsController : ControllerBase
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly DbAgentSkillsSource _skillsSource;

    public AgentSkillsController(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        DbAgentSkillsSource skillsSource)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _skillsSource = skillsSource ?? throw new ArgumentNullException(nameof(skillsSource));
    }

    /// <summary>
    /// Retorna todas as skills associadas ao Tenant logado. 
    /// Executa automaticamente o auto-seeding se o catálogo estiver vazio.
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllSkills()
    {
        await _skillsSource.LoadSkillsAsync();
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var skills = await db.AgentSkills.AsNoTracking().OrderBy(item => item.Name).ToListAsync();
        var summary = skills.Select(s => new
        {
            s.Id,
            s.Name,
            s.Domain,
            type = s.Type,
            isSystem = s.IsSystem,
            isEnabled = s.IsEnabled,
            canManage = User.IsInRole("Owner") || User.IsInRole("Admin")
        });

        return Ok(summary);
    }

    /// <summary>
    /// Busca e retorna o prompt e metadados detalhados de uma única skill.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetSkillById(string id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.AgentSkills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        
        if (entity == null)
        {
            return NotFound(new { error = $"Skill '{id}' não encontrada." });
        }

        return Ok(new
        {
            entity.Id,
            entity.Name,
            entity.Domain,
            type = entity.Type,
            isSystem = entity.IsSystem,
            isEnabled = entity.IsEnabled,
            canManage = User.IsInRole("Owner") || User.IsInRole("Admin"),
            systemPrompt = entity.SystemPromptFragment,
            examples = entity.FewShotExamples,
            metadata = string.IsNullOrEmpty(entity.MetadataJson) 
                ? new Dictionary<string, string>() 
                : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson)
        });
    }

    /// <summary>
    /// Cria uma nova skill customizada via formulário JSON na interface.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> CreateSkill([FromBody] CreateSkillRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("ID e Nome da habilidade são obrigatórios.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        // Evita colisão de IDs para o mesmo Tenant
        var idNormalized = request.Id.ToLowerInvariant().Trim();
        var exists = await db.AgentSkills.AnyAsync(s => s.Id == idNormalized);
        if (exists)
        {
            return Conflict(new { error = $"Uma skill com o identificador '{idNormalized}' já existe neste tenant." });
        }

        var entity = new DbSkillEntity
        {
            Id = idNormalized,
            Name = request.Name.Trim(),
            Domain = string.IsNullOrWhiteSpace(request.Domain) ? "general" : request.Domain.ToLowerInvariant().Trim(),
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Instruction" : request.Type.Trim(),
            SystemPromptFragment = request.SystemPromptFragment ?? string.Empty,
            FewShotExamples = request.FewShotExamples,
            IsSystem = false, // Habilidade criada via tela é sempre Customizada
            MetadataJson = JsonSerializer.Serialize(request.Metadata ?? new())
        };

        db.AgentSkills.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSkillById), new { id = entity.Id }, entity);
    }

    /// <summary>
    /// Atualiza uma skill customizada existente. Bloqueia edições contra skills de Sistema.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> UpdateSkill(string id, [FromBody] UpdateSkillRequest request)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.AgentSkills.FindAsync(id);
        
        if (entity == null)
        {
            return NotFound(new { error = $"Skill '{id}' não encontrada." });
        }

        // BLOQUEIO DE SEGURANÇA: Skills de Sistema são read-only
        if (entity.IsSystem)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Habilidades nativas de sistema não podem ser alteradas." });
        }

        entity.Name = request.Name?.Trim() ?? entity.Name;
        entity.Domain = request.Domain?.ToLowerInvariant().Trim() ?? entity.Domain;
        entity.SystemPromptFragment = request.SystemPromptFragment ?? entity.SystemPromptFragment;
        entity.FewShotExamples = request.FewShotExamples ?? entity.FewShotExamples;
        entity.UpdatedAt = DateTime.UtcNow;

        if (request.Metadata != null)
        {
            entity.MetadataJson = JsonSerializer.Serialize(request.Metadata);
        }

        await db.SaveChangesAsync();
        return Ok(entity);
    }

    /// <summary>
    /// Exclui uma skill customizada. Bloqueia exclusões contra skills de Sistema.
    /// </summary>
    [HttpPut("{id}/enabled")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> SetSkillEnabled(string id, [FromBody] SetSkillEnabledRequest request, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.AgentSkills.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (entity is null) return NotFound(new { error = $"Skill '{id}' não encontrada." });
        entity.IsEnabled = request.Enabled;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { entity.Id, isEnabled = entity.IsEnabled });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> DeleteSkill(string id)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.AgentSkills.FindAsync(id);
        
        if (entity == null)
        {
            return NotFound(new { error = $"Skill '{id}' não encontrada." });
        }

        // BLOQUEIO DE SEGURANÇA: Skills de Sistema são read-only
        if (entity.IsSystem)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Habilidades nativas de sistema não podem ser excluídas." });
        }

        db.AgentSkills.Remove(entity);
        await db.SaveChangesAsync();
        
        return NoContent();
    }

    /// <summary>
    /// Rota que disponibiliza o download do template padrão Markdown de Skills para edição offline.
    /// </summary>
    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        var template = @"---
id: custom-skill-id
name: Minha Habilidade Customizada
domain: general
type: Instruction
metadata: { ""category"": ""custom"", ""author"": ""user"" }
---

# Instruções do Sistema
Escreva aqui as diretrizes fundamentais de prompt da habilidade que guiarão o agente.
Exemplo:
- Sempre responda de forma resumida estruturando em bullet points.
- Utilize um tom profissional, amigável e direto.
- Se não souber a resposta, declare honestamente.

# Exemplos de Execução (Opcional)
Aqui você pode inserir exemplos few-shot de interações para calibração.

User: Como calcular a margem bruta de lucro?
Agent: A margem bruta é calculada através da fórmula: `(Receita Bruta - Custo dos Bens Vendidos) / Receita Bruta * 100`.";

        var bytes = System.Text.Encoding.UTF8.GetBytes(template);
        return File(bytes, "text/markdown", "skill-template.md");
    }

    /// <summary>
    /// Rota que aceita o upload de arquivos Markdown, realiza o parsing e grava a skill no PostgreSQL.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Roles = "Owner,Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadSkill(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "O arquivo enviado está vazio." });
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        // Regex para extrair Frontmatter YAML demarcado por ---
        var match = Regex.Match(content, @"^---\r?\n(.*?)\r?\n---\r?\n(.*)$", RegexOptions.Singleline);
        if (!match.Success)
        {
            return BadRequest(new { error = "Arquivo Markdown inválido. Certifique-se de incluir a seção 'frontmatter' demarcada por '---' no início do arquivo." });
        }

        var frontmatter = match.Groups[1].Value;
        var promptBody = match.Groups[2].Value.Trim();

        var id = ExtractValue(frontmatter, "id") ?? Path.GetFileNameWithoutExtension(file.FileName);
        var name = ExtractValue(frontmatter, "name") ?? id;
        var domain = ExtractValue(frontmatter, "domain") ?? "general";
        var typeStr = ExtractValue(frontmatter, "type") ?? "Instruction";

        id = id.ToLowerInvariant().Trim();

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        // Valida colisão de ID
        var exists = await db.AgentSkills.AnyAsync(s => s.Id == id);
        if (exists)
        {
            return Conflict(new { error = $"Uma habilidade com o ID '{id}' já existe neste tenant. Edite o ID no arquivo ou na tela antes do envio." });
        }

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
            catch {}
        }

        var entity = new DbSkillEntity
        {
            Id = id,
            Name = name.Trim(),
            Domain = domain.ToLowerInvariant().Trim(),
            Type = typeStr.Trim(),
            SystemPromptFragment = promptBody,
            IsSystem = false, // Habilidade por upload é sempre customizada
            MetadataJson = JsonSerializer.Serialize(metadata)
        };

        db.AgentSkills.Add(entity);
        await db.SaveChangesAsync();

        return Ok(new { message = "Skill importada e persistida no banco com sucesso!", id });
    }

    /// <summary>
    /// Rota conversacional para auxiliar na modelagem de prompts de skills via IA (Brainstorming).
    /// </summary>
    [HttpPost("brainstorm")]
    public async Task<IActionResult> BrainstormSkill([FromBody] BrainstormRequest request)
    {
        // Esta rota pode atuar integrando um prompt dinâmico ao chat principal
        // Retorna uma sugestão estruturada baseada na descrição do usuário
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("A descrição da habilidade é necessária.");
        }

        // Aqui, podemos delegar para o serviço principal de chat para processar
        return Ok(new
        {
            suggestedId = Regex.Replace(request.Description.ToLowerInvariant().Split(' ')[0], @"[^a-z0-9]", ""),
            suggestedName = "Habilidade Gerada por IA",
            systemPromptFragment = $"# Instruções do Sistema\nVocê é um especialista em {request.Description}.\n- Adote uma abordagem analítica.\n- Garanta respostas claras.",
            fewShotExamples = "User: Dúvida de exemplo\nAgent: Resposta explicada passo a passo."
        });
    }

    private static string? ExtractValue(string frontmatter, string key)
    {
        var match = Regex.Match(frontmatter, $@"{key}:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim().Trim('"', '\'') : null;
    }
}

public sealed record SetSkillEnabledRequest(bool Enabled);

public class CreateSkillRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = "general";
    public string Type { get; set; } = "Instruction";
    public string SystemPromptFragment { get; set; } = string.Empty;
    public string? FewShotExamples { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class UpdateSkillRequest
{
    public string? Name { get; set; }
    public string? Domain { get; set; }
    public string? SystemPromptFragment { get; set; }
    public string? FewShotExamples { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class BrainstormRequest
{
    public string Description { get; set; } = string.Empty;
}
