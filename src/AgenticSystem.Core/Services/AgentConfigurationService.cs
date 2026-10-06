using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class AgentConfigurationService : IAgentConfigurationService
{
    private readonly IAgentYamlValidator _yamlValidator;
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentVersioningService? _versioningService;
    private readonly ITenantContextAccessor? _tenantContextAccessor;
    private readonly ITenantIsolationEnforcer? _tenantIsolationEnforcer;
    private readonly ILogger<AgentConfigurationService> _logger;

    public AgentConfigurationService(
        IAgentYamlValidator yamlValidator,
        IAgentFactory agentFactory,
        ILogger<AgentConfigurationService> logger,
        IAgentVersioningService? versioningService = null,
        ITenantContextAccessor? tenantContextAccessor = null,
        ITenantIsolationEnforcer? tenantIsolationEnforcer = null)
    {
        _yamlValidator = yamlValidator ?? throw new ArgumentNullException(nameof(yamlValidator));
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _versioningService = versioningService;
        _tenantContextAccessor = tenantContextAccessor;
        _tenantIsolationEnforcer = tenantIsolationEnforcer;
    }

    public async Task<AgentConfigurationResult> SaveAgentFromYamlAsync(string yaml, string requestedBy = "System", CancellationToken ct = default)
    {
        var result = await _yamlValidator.ValidateAsync(yaml, ct);
        if (!result.IsValid || result.Specification is null)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Message));
            return new AgentConfigurationResult(false, $"YAML Validation failed: {errors}");
        }

        var spec = result.Specification;

        var tenantId = _tenantContextAccessor?.CurrentTenantId;
        if (_tenantIsolationEnforcer is not null && !string.IsNullOrWhiteSpace(tenantId) &&
            !await _tenantIsolationEnforcer.CanCreateAgentAsync(tenantId, spec.Name, ct))
        {
            return new AgentConfigurationResult(false, "O limite de agentes deste tenant foi atingido.");
        }
        
        // Remove agente existente se houver
        var agents = await _agentFactory.GetAllAgentsAsync();
        var existing = agents.FirstOrDefault(a => a.Name.Equals(spec.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            await _agentFactory.RemoveAgentAsync(spec.Name);
        }

        var agent = await _agentFactory.CreateCustomAgentAsync(spec);

        AgentVersion? version = null;
        if (_versioningService is not null)
        {
            try
            {
                version = await _versioningService.CreateVersionAsync(
                    spec.Name,
                    description: "Salvo via YAML Declarativo",
                    changeLog: "Declarative YAML Update",
                    createdBy: requestedBy,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao registrar a versão do agente {Name} no versioning store.", spec.Name);
            }
        }

        var agentInfo = new AgentInfo
        {
            Name = agent.Name,
            Description = agent.Description,
            Tier = agent.Tier,
            Domain = agent.Domain,
            Instructions = agent.Instructions,
            Capabilities = spec.Capabilities.ToList(),
            Configuration = new(spec.Configuration),
            AvailableTools = agent.AvailableTools.ToList(),
            AutonomyLevel = agent.AutonomyLevel,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt
        };

        return new AgentConfigurationResult(true, "Agente salvo com sucesso.", agentInfo, version);
    }

    public async Task<AgentConfigurationResult> RollbackAgentAsync(string name, string versionId, string rolledBackBy = "System", CancellationToken ct = default)
    {
        if (_versioningService is null)
        {
            return new AgentConfigurationResult(false, "Serviço de versionamento não disponível.");
        }

        var rollbackResult = await _versioningService.RollbackAsync(name, versionId, rolledBackBy, ct);
        if (!rollbackResult.Success || rollbackResult.Version is null)
        {
            return new AgentConfigurationResult(false, rollbackResult.Message ?? "Erro desconhecido");
        }

        var targetVersion = rollbackResult.Version;
        var spec = new AgentSpecification
        {
            Name = name,
            Instructions = targetVersion.SystemPrompt ?? string.Empty,
            AllowedTools = targetVersion.Tools?.ToList() ?? new System.Collections.Generic.List<string>(),
            Description = targetVersion.Description ?? "Versão restaurada via rollback"
        };

        if (targetVersion.Parameters is not null)
        {
            if (targetVersion.Parameters.TryGetValue("model", out var modelObj) && modelObj is not null)
            {
                spec.Configuration["model"] = modelObj.ToString()!;
            }
            if (targetVersion.Parameters.TryGetValue("temperature", out var tempObj) && tempObj is not null && double.TryParse(tempObj.ToString(), out var tempVal))
            {
                spec.Configuration["temperature"] = tempVal;
            }
        }

        var agents = await _agentFactory.GetAllAgentsAsync();
        var existing = agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            spec.Tier = existing.Tier;
            spec.Domain = existing.Domain;
            spec.AutonomyLevel = existing.AutonomyLevel;
        }

        await _agentFactory.RemoveAgentAsync(name);
        var restoredAgent = await _agentFactory.CreateCustomAgentAsync(spec);

        var agentInfo = new AgentInfo
        {
            Name = restoredAgent.Name,
            Description = restoredAgent.Description,
            Tier = restoredAgent.Tier,
            IsActive = restoredAgent.IsActive,
            CreatedAt = restoredAgent.CreatedAt
        };

        return new AgentConfigurationResult(true, rollbackResult.Message ?? "Rollback com sucesso", agentInfo, targetVersion);
    }
}
