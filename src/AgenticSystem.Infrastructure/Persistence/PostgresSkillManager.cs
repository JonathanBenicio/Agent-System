using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Persistence.Entities;
using AgenticSystem.Infrastructure.AgentFramework;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresSkillManager : ISkillManager
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresSkillManager> _logger;
    private readonly DbAgentSkillsSource _skillsSource;
    private readonly ITenantContextAccessor _tenantAccessor;

    public PostgresSkillManager(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        DbAgentSkillsSource skillsSource,
        ILogger<PostgresSkillManager> logger,
        ITenantContextAccessor tenantAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _skillsSource = skillsSource;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IEnumerable<SkillContent>> GetSkillsForAgentAsync(string agentName, string domain)
    {
        string? activeTenantId = null;
        try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = activeTenantId ?? "system-background" });

        var skills = await _skillsSource.LoadSkillsAsync();
        var relevantSkills = skills
            .Where(s => s.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                        s.Domain.Equals("general", StringComparison.OrdinalIgnoreCase));

        var contents = new List<SkillContent>();
        foreach (var skill in relevantSkills)
        {
            var context = new SkillContext { AgentName = agentName, Domain = domain };
            contents.Add(await skill.GetContentAsync(context));
        }

        return contents;
    }

    public async Task<string> BuildEnrichedPromptAsync(string agentName, string domain, string basePrompt)
    {
        var skills = await GetSkillsForAgentAsync(agentName, domain);
        var skillFragments = skills
            .Select(s => s.SystemPromptFragment)
            .Where(f => !string.IsNullOrWhiteSpace(f));

        var fragments = string.Join("\n\n", skillFragments);
        return string.IsNullOrWhiteSpace(fragments)
            ? basePrompt
            : $"{basePrompt}\n\n--- Knowledge Context ---\n{fragments}";
    }

    public void RegisterSkill(ISkill skill)
    {
        string? activeTenantId = null;
        try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = activeTenantId ?? "system-background" });

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var entity = db.AgentSkills.IgnoreQueryFilters().FirstOrDefault(s => s.Id == skill.Id);
            var content = skill.GetContentAsync(new SkillContext()).GetAwaiter().GetResult();
            if (entity == null)
            {
                entity = new DbSkillEntity
                {
                    Id = skill.Id,
                    TenantId = db.CurrentTenantId ?? "default",
                    Name = skill.Name,
                    Domain = skill.Domain,
                    Type = skill.Type.ToString(),
                    SystemPromptFragment = content.SystemPromptFragment,
                    FewShotExamples = content.FewShotExamples,
                    IsSystem = skill is DbBasedSkill dbBased ? dbBased.IsSystem : false,
                    MetadataJson = content.Metadata != null ? JsonSerializer.Serialize(content.Metadata) : "{}"
                };
                db.AgentSkills.Add(entity);
            }
            else
            {
                entity.Name = skill.Name;
                entity.Domain = skill.Domain;
                entity.Type = skill.Type.ToString();
                entity.SystemPromptFragment = content.SystemPromptFragment;
                entity.FewShotExamples = content.FewShotExamples;
                entity.MetadataJson = content.Metadata != null ? JsonSerializer.Serialize(content.Metadata) : "{}";
                entity.UpdatedAt = DateTime.UtcNow;
            }
            db.SaveChanges();
            _logger.LogInformation("📚 Postgres Skill registered/synced: {SkillName} ({Domain})", skill.Name, skill.Domain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register skill {SkillId} in PostgreSQL", skill.Id);
        }
    }

    public bool UnregisterSkill(string skillId)
    {
        string? activeTenantId = null;
        try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = activeTenantId ?? "system-background" });

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var entity = db.AgentSkills.FirstOrDefault(s => s.Id == skillId);
            if (entity != null)
            {
                db.AgentSkills.Remove(entity);
                db.SaveChanges();
                _logger.LogInformation("📚 Postgres Skill removed: {SkillId}", skillId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister skill {SkillId} in PostgreSQL", skillId);
            return false;
        }
    }

    public IEnumerable<ISkill> GetAllSkills()
    {
        string? activeTenantId = null;
        try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = activeTenantId ?? "system-background" });

        try
        {
            return _skillsSource.LoadSkillsAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all skills from PostgreSQL");
            return Enumerable.Empty<ISkill>();
        }
    }
}
