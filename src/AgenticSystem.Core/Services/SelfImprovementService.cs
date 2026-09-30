using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Core.Services;

public sealed class SelfImprovementService : ISelfImprovementEngine
{
    private readonly IOperationalStore _operationalStore;
    private readonly ISelfImprovementProposalStore _proposalStore;
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentVersionStore _agentVersionStore;
    private readonly IPromptManager _promptManager;
    private readonly IAuditLog _auditLog;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly SelfImprovementSettings _settings;
    private const string CursorKey = "SelfImprovement_LastReflectionId";

    public SelfImprovementService(
        IOperationalStore operationalStore,
        ISelfImprovementProposalStore proposalStore,
        IAgentFactory agentFactory,
        IAgentVersionStore agentVersionStore,
        IPromptManager promptManager,
        IAuditLog auditLog,
        ITenantContextAccessor tenantContextAccessor,
        IOptions<SelfImprovementSettings> options)
    {
        _operationalStore = operationalStore;
        _proposalStore = proposalStore;
        _agentFactory = agentFactory;
        _agentVersionStore = agentVersionStore;
        _promptManager = promptManager;
        _auditLog = auditLog;
        _tenantContextAccessor = tenantContextAccessor;
        _settings = options.Value;
    }

    public async Task ProcessBatchImprovementsAsync(CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var cursorState = await _operationalStore.GetSystemStateAsync(CursorKey, ct);
        var newReflections = await _operationalStore.GetReflectionsSinceAsync(cursorState?.Value, 500, ct);
        var agentsToProcess = newReflections
            .Where(reflection => reflection.Severity == ReflectionSeverity.Critical)
            .GroupBy(reflection => reflection.AgentName)
            .ToList();

        foreach (var group in agentsToProcess)
            await AnalyzeAndImproveAsync(group.Key, ct);

        if (newReflections.Count > 0)
        {
            await _operationalStore.SaveSystemStateAsync(new SystemState
            {
                Id = CursorKey,
                Value = newReflections[^1].Id
            }, ct);
        }
    }

    public async Task<SelfImprovementRecord> AnalyzeAndImproveAsync(string agentName, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return new SelfImprovementRecord { AgentName = agentName, Status = "Disabled" };

        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var reflections = await _operationalStore.GetRecentLearningsAsync(50, ct);
        var criticalReflections = reflections
            .Where(reflection => reflection.AgentName == agentName && reflection.Severity == ReflectionSeverity.Critical)
            .ToList();
        var currentInstructions = await _promptManager.ResolvePromptAsync(agentName, ct: ct);
        var record = new SelfImprovementRecord
        {
            TenantId = tenantId,
            AgentName = agentName,
            Type = ImprovementType.PromptRefinement,
            Rationale = $"Analyzing {criticalReflections.Count} critical reflections.",
            CreatedBy = "SelfImprovementEngine",
            PreviousInstructions = currentInstructions
        };

        if (criticalReflections.Count == 0)
        {
            record.Status = "NoImprovementNeeded";
            record.ConfidenceLevel = 1.0;
            record.Rationale = "Current performance is stable.";
            return record;
        }

        record.ConfidenceLevel = Math.Min(0.5 + criticalReflections.Count * 0.1, 0.95);
        var lessons = string.Join("; ", criticalReflections
            .SelectMany(reflection => reflection.LessonsLearned)
            .Distinct(StringComparer.Ordinal)
            .Take(5));
        record.ProposedChanges["instructions"] = string.IsNullOrWhiteSpace(currentInstructions)
            ? $"Apply these reviewed lessons to your behavior: {lessons}"
            : $"{currentInstructions.TrimEnd()}\n\n## Proposed improvement (requires tenant approval)\nApply these reviewed lessons to your behavior: {lessons}";
        record.Status = "Proposed";

        await _proposalStore.SaveAsync(record, ct);
        await _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.ConfigChange,
            Action = "SelfImprovement.Proposed",
            TenantId = tenantId,
            AgentName = agentName,
            Description = "Self-improvement proposal created for human review.",
            Metadata = new Dictionary<string, object>
            {
                ["proposalId"] = record.Id,
                ["confidenceLevel"] = record.ConfidenceLevel
            }
        }, ct);

        return record;
    }

    public Task<IReadOnlyList<SelfImprovementRecord>> GetProposalsAsync(CancellationToken ct = default) =>
        _proposalStore.GetAllAsync(ct);

    public async Task<bool> ApproveProposalAsync(string improvementId, string approvedBy, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return false;

        var proposal = await GetProposedRecordAsync(improvementId, ct);
        if (proposal is null || !proposal.ProposedChanges.TryGetValue("instructions", out var newInstructions))
            return false;

        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var agent = await GetAgentInfoAsync(proposal.AgentName);
        if (agent is null)
            return false;

        await _agentFactory.CreateCustomAgentAsync(ToSpecification(agent, newInstructions));
        var versions = await _promptManager.GetTemplatesAsync(proposal.AgentName, ct);
        var nextVersion = versions.Count == 0 ? 1 : versions.Max(template => template.Version) + 1;
        await _promptManager.SaveTemplateAsync(new PromptTemplate
        {
            Name = "Approved self-improvement",
            AgentName = proposal.AgentName,
            TenantId = tenantId,
            TemplateBody = newInstructions,
            Version = nextVersion,
            Description = $"Approved proposal {proposal.Id}",
            CreatedBy = approvedBy
        }, ct);
        var agentVersion = await SaveActiveAgentVersionAsync(
            agent,
            newInstructions,
            proposal.AgentName,
            $"Approved self-improvement proposal {proposal.Id}",
            proposal.Rationale ?? string.Empty,
            approvedBy,
            ct);

        proposal.Status = "Applied";
        proposal.ReviewedBy = approvedBy;
        proposal.ReviewedAt = DateTime.UtcNow;
        proposal.AppliedPromptVersion = nextVersion;
        proposal.AppliedAgentVersionId = agentVersion.Id;
        await _proposalStore.UpdateAsync(proposal, ct);
        await RecordReviewAsync(proposal, "SelfImprovement.Approved", approvedBy, ct);
        return true;
    }

    public async Task<bool> RejectProposalAsync(string improvementId, string rejectedBy, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return false;

        var proposal = await GetProposedRecordAsync(improvementId, ct);
        if (proposal is null)
            return false;

        proposal.Status = "Rejected";
        proposal.ReviewedBy = rejectedBy;
        proposal.ReviewedAt = DateTime.UtcNow;
        await _proposalStore.UpdateAsync(proposal, ct);
        await RecordReviewAsync(proposal, "SelfImprovement.Rejected", rejectedBy, ct);
        return true;
    }

    public async Task<bool> RollbackProposalAsync(string improvementId, string rolledBackBy, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return false;

        var proposal = await _proposalStore.GetAsync(improvementId, ct);
        if (proposal is null || proposal.Status != "Applied" || proposal.PreviousInstructions is null)
            return false;

        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var agent = await GetAgentInfoAsync(proposal.AgentName);
        if (agent is null)
            return false;

        await _agentFactory.CreateCustomAgentAsync(ToSpecification(agent, proposal.PreviousInstructions));
        var versions = await _promptManager.GetTemplatesAsync(proposal.AgentName, ct);
        var nextVersion = versions.Count == 0 ? 1 : versions.Max(template => template.Version) + 1;
        await _promptManager.SaveTemplateAsync(new PromptTemplate
        {
            Name = "Self-improvement rollback",
            AgentName = proposal.AgentName,
            TenantId = tenantId,
            TemplateBody = proposal.PreviousInstructions,
            Version = nextVersion,
            Description = $"Rollback of proposal {proposal.Id}",
            CreatedBy = rolledBackBy
        }, ct);
        var agentVersion = await SaveActiveAgentVersionAsync(
            agent,
            proposal.PreviousInstructions,
            proposal.AgentName,
            $"Rollback of self-improvement proposal {proposal.Id}",
            "Restored instructions from before the approved proposal.",
            rolledBackBy,
            ct);

        proposal.Status = "RolledBack";
        proposal.ReviewedBy = rolledBackBy;
        proposal.ReviewedAt = DateTime.UtcNow;
        proposal.AppliedPromptVersion = nextVersion;
        proposal.AppliedAgentVersionId = agentVersion.Id;
        await _proposalStore.UpdateAsync(proposal, ct);
        await RecordReviewAsync(proposal, "SelfImprovement.RolledBack", rolledBackBy, ct);
        return true;
    }

    private async Task<SelfImprovementRecord?> GetProposedRecordAsync(string proposalId, CancellationToken ct)
    {
        var proposal = await _proposalStore.GetAsync(proposalId, ct);
        return proposal?.Status == "Proposed" ? proposal : null;
    }

    private async Task<AgentInfo?> GetAgentInfoAsync(string agentName)
    {
        var agents = await _agentFactory.GetAllAgentsAsync();
        return agents.FirstOrDefault(agent => agent.Name.Equals(agentName, StringComparison.OrdinalIgnoreCase));
    }

    private static AgentSpecification ToSpecification(AgentInfo agent, string instructions) => new()
    {
        Name = agent.Name,
        Description = agent.Description,
        Tier = agent.Tier,
        Domain = agent.Domain,
        AllowedTools = agent.AvailableTools.ToList(),
        AutonomyLevel = agent.AutonomyLevel,
        Instructions = instructions
    };

    private async Task<AgentVersion> SaveActiveAgentVersionAsync(
        AgentInfo agent,
        string instructions,
        string agentName,
        string description,
        string changeLog,
        string createdBy,
        CancellationToken ct)
    {
        var activeVersion = await _agentVersionStore.GetActiveAsync(agentName, AgentVersionEnvironment.Production, ct);
        if (activeVersion is not null)
        {
            activeVersion.Status = AgentVersionStatus.Deprecated;
            await _agentVersionStore.SaveAsync(activeVersion, ct);
        }

        var nextVersion = await _agentVersionStore.GetNextVersionNumberAsync(agentName, ct);
        var version = new AgentVersion
        {
            AgentName = agentName,
            TenantId = _tenantContextAccessor.CurrentTenantId,
            VersionNumber = nextVersion,
            Label = $"v{nextVersion}.0",
            Status = AgentVersionStatus.Active,
            Environment = AgentVersionEnvironment.Production,
            SystemPrompt = instructions,
            Tools = agent.AvailableTools,
            Description = description,
            ChangeLog = changeLog,
            CreatedBy = createdBy,
            ParentVersionId = activeVersion?.Id
        };
        await _agentVersionStore.SaveAsync(version, ct);
        await _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.ConfigChange,
            Action = "AgentVersion.Created",
            UserId = createdBy,
            TenantId = version.TenantId,
            AgentName = agentName,
            Description = description,
            Metadata = new Dictionary<string, object>
            {
                ["versionId"] = version.Id,
                ["versionNumber"] = version.VersionNumber,
                ["parentVersionId"] = version.ParentVersionId ?? string.Empty
            }
        }, ct);
        return version;
    }

    private Task RecordReviewAsync(SelfImprovementRecord proposal, string action, string userId, CancellationToken ct) =>
        _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.ApprovalDecision,
            Action = action,
            UserId = userId,
            TenantId = proposal.TenantId,
            AgentName = proposal.AgentName,
            Description = $"Self-improvement proposal {proposal.Id} {proposal.Status.ToLowerInvariant()}.",
            Metadata = new Dictionary<string, object>
            {
                ["proposalId"] = proposal.Id,
                ["promptVersion"] = proposal.AppliedPromptVersion ?? 0
            }
        }, ct);
}
