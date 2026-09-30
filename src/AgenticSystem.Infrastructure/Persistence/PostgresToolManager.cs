using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresToolManager : IToolManager
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new();
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly IToolGovernanceService? _toolGovernance;
    private readonly IAgentRuntimeCoordinator? _runtimeCoordinator;
    private readonly ILogger<PostgresToolManager> _logger;
    private readonly ITenantContextAccessor _tenantAccessor;

    public PostgresToolManager(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresToolManager> logger,
        ITenantContextAccessor tenantAccessor,
        IToolGovernanceService? toolGovernance = null,
        IAgentRuntimeCoordinator? runtimeCoordinator = null)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
        _toolGovernance = toolGovernance;
        _runtimeCoordinator = runtimeCoordinator;
    }

    public async Task<ToolResult> ExecuteToolAsync(string toolId, ToolInput input, CancellationToken ct = default)
    {
        var registration = await ResolveRegistrationAsync(toolId, input, ct);
        var tool = registration?.Tool;

        if (tool is null && !_tools.TryGetValue(toolId, out tool))
        {
            _logger.LogWarning("🔧 Tool não encontrada no PostgresToolManager: {ToolId}", toolId);
            return ToolResult.Fail($"Tool '{toolId}' não encontrada.");
        }

        var decision = _toolGovernance is not null
            ? await _toolGovernance.EvaluateAsync(tool, input, ct)
            : new ToolExecutionDecision
            {
                Allowed = true,
                Policy = new ToolExecutionPolicy { ToolId = toolId }
            };

        if (!decision.Allowed)
        {
            return ToolResult.Fail(decision.Reason, BuildDecisionMetadata(decision));
        }

        if (!await tool.IsAvailableAsync(ct))
        {
            _logger.LogWarning("🔧 Tool indisponível: {ToolId}", toolId);
            return ToolResult.Fail($"Tool '{toolId}' está indisponível.");
        }

        _logger.LogInformation("🔧 Executando tool no PostgresToolManager: {ToolId} | Action: {Action}", toolId, input.Action);
        await PublishToolEventAsync(AgentStreamEventType.ToolStarted, tool, input.Action, null, true, null, ct);

        Exception? lastError = null;
        for (var attempt = 0; attempt <= decision.Policy.MaxRetries; attempt++)
        {
            var sw = Stopwatch.StartNew();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(decision.Policy.Timeout);

            try
            {
                var result = await tool.ExecuteAsync(input, timeoutCts.Token);
                sw.Stop();

                if (_runtimeCoordinator is not null)
                {
                    await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
                    {
                        SessionId = _runtimeCoordinator.CurrentSessionId ?? string.Empty,
                        Type = AgentExecutionArtifactType.ToolExecution,
                        Name = tool.Name,
                        AgentName = _runtimeCoordinator.CurrentAgentName,
                        Status = result.Success ? "Success" : "Failed",
                        Summary = result.ErrorMessage,
                        Data = new Dictionary<string, object>
                        {
                            ["toolId"] = tool.Id,
                            ["logicalToolId"] = toolId,
                            ["toolVersion"] = registration?.Version ?? "1.0.0",
                            ["toolVariant"] = registration?.VariantName ?? "default",
                            ["action"] = input.Action,
                            ["attempt"] = attempt + 1,
                            ["latencyMs"] = sw.Elapsed.TotalMilliseconds,
                            ["metadata"] = result.Metadata ?? new Dictionary<string, object>()
                        }
                    }, ct);
                }

                await PublishToolEventAsync(AgentStreamEventType.ToolCompleted, tool, input.Action, sw.Elapsed.TotalMilliseconds, result.Success, result.Metadata, ct);
                _logger.LogInformation("🔧 Tool {ToolId} executada com sucesso: {Success}", toolId, result.Success);

                var metadata = result.Metadata is null
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(result.Metadata);
                metadata["logicalToolId"] = toolId;
                metadata["toolVersion"] = registration?.Version ?? "1.0.0";
                metadata["toolVariant"] = registration?.VariantName ?? "default";
                metadata["selectedToolId"] = tool.Id;

                return result with { Metadata = metadata };
            }
            catch (Exception ex)
            {
                sw.Stop();
                lastError = ex;

                if (attempt >= decision.Policy.MaxRetries)
                {
                    await PublishToolEventAsync(AgentStreamEventType.Error, tool, input.Action, sw.Elapsed.TotalMilliseconds, false, new Dictionary<string, object>
                    {
                        ["toolId"] = tool.Id,
                        ["attempt"] = attempt + 1,
                        ["fallback"] = false,
                        ["error"] = ex.Message
                    }, ct);
                    _logger.LogError(ex, "🔧 Erro ao executar tool {ToolId}: {Message}", toolId, ex.Message);
                    return ToolResult.Fail($"Erro ao executar '{toolId}': {ex.Message}", new Dictionary<string, object>
                    {
                        ["toolId"] = tool.Id,
                        ["attempts"] = attempt + 1
                    });
                }
            }
        }

        return ToolResult.Fail($"Erro ao executar '{toolId}': {lastError?.Message}");
    }

    public async Task<IEnumerable<ITool>> GetAvailableToolsAsync(string? category = null)
    {
        await SyncFromDbAsync();

        IEnumerable<ITool> tools = _tools.Values;

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ToolCategory>(category, true, out var cat))
        {
            tools = tools.Where(t => t.Category == cat);
        }

        return tools;
    }

    public void RegisterTool(ITool tool)
    {
        _tools[tool.Id] = tool;
        RegisterToolVariant(tool.Id, tool, version: "1.0.0", isDefault: true);
    }

    public void RegisterToolVariant(
        string logicalToolId,
        ITool tool,
        string version,
        string? variantName = null,
        int rolloutPercentage = 100,
        bool isDefault = false)
    {
        _tools[tool.Id] = tool;

        string? activeTenantId = null;
        try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = activeTenantId ?? "system-background" });

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var entity = db.AgentTools.IgnoreQueryFilters().FirstOrDefault(t => t.Id == tool.Id && t.Version == version);
            if (entity == null)
            {
                entity = new DbToolEntity
                {
                    Id = tool.Id,
                    TenantId = db.CurrentTenantId,
                    Name = tool.Name,
                    Description = tool.Description,
                    Category = tool.Category.ToString(),
                    RequiresAuth = tool.RequiresAuth,
                    Type = "Builtin",
                    Version = version,
                    VariantName = variantName,
                    RolloutPercentage = rolloutPercentage,
                    IsDefault = isDefault
                };
                db.AgentTools.Add(entity);
            }
            else
            {
                entity.Name = tool.Name;
                entity.Description = tool.Description;
                entity.Category = tool.Category.ToString();
                entity.RequiresAuth = tool.RequiresAuth;
                entity.VariantName = variantName;
                entity.RolloutPercentage = rolloutPercentage;
                entity.IsDefault = isDefault;
                entity.UpdatedAt = DateTime.UtcNow;
            }

            db.SaveChanges();
            _logger.LogInformation("🔧 Postgres Tool registered/synced: {ToolName}", tool.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist tool {ToolId} in PostgreSQL", tool.Id);
        }
    }

    public async Task<IReadOnlyList<ToolRegistration>> GetRegistrationsAsync(string logicalToolId, CancellationToken ct = default)
    {
        string? activeTenantId = null;
        try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = activeTenantId ?? "system-background" });

        await SyncFromDbAsync(ct);

        using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entities = await db.AgentTools
            .Where(t => t.Id.StartsWith(logicalToolId))
            .ToListAsync(ct);

        return entities.Select(e => new ToolRegistration
        {
            LogicalToolId = logicalToolId,
            Tool = _tools.TryGetValue(e.Id, out var t) ? t : new DummyDbTool(e),
            Version = e.Version,
            VariantName = e.VariantName,
            RolloutPercentage = e.RolloutPercentage,
            IsDefault = e.IsDefault
        }).ToList();
    }

    public bool UnregisterTool(string toolId)
    {
        var removed = _tools.TryRemove(toolId, out _);

        string? activeTenantId = null;
        try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = activeTenantId ?? "system-background" });

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var entities = db.AgentTools.Where(t => t.Id == toolId).ToList();
            if (entities.Count > 0)
            {
                db.AgentTools.RemoveRange(entities);
                db.SaveChanges();
                _logger.LogInformation("🔧 Postgres Tool removed: {ToolId}", toolId);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister tool {ToolId} in PostgreSQL", toolId);
        }

        return removed;
    }

    public ITool? GetTool(string toolId)
    {
        _tools.TryGetValue(toolId, out var tool);
        return tool;
    }

    private async Task SyncFromDbAsync(CancellationToken ct = default)
    {
        string? activeTenantId = null;
        try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = activeTenantId ?? "system-background" });

        try
        {
            using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var entities = await db.AgentTools.AsNoTracking().ToListAsync(ct);

            foreach (var entity in entities)
            {
                if (!_tools.ContainsKey(entity.Id))
                {
                    _tools[entity.Id] = new DummyDbTool(entity);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing tools from PostgreSQL");
        }
    }

    private async Task<ToolRegistration?> ResolveRegistrationAsync(string logicalToolId, ToolInput input, CancellationToken ct)
    {
        var registrations = await GetRegistrationsAsync(logicalToolId, ct);
        if (registrations.Count == 0) return null;

        return registrations.FirstOrDefault(r => r.IsDefault) ?? registrations.First();
    }

    private async Task PublishToolEventAsync(
        AgentStreamEventType type,
        ITool tool,
        string action,
        double? latencyMs,
        bool success,
        Dictionary<string, object>? metadata,
        CancellationToken ct)
    {
        if (_runtimeCoordinator is null) return;

        var payload = metadata is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(metadata);
        payload["toolId"] = tool.Id;
        payload["toolName"] = tool.Name;
        payload["action"] = action;
        payload["success"] = success;

        if (latencyMs.HasValue)
        {
            payload["latencyMs"] = latencyMs.Value;
        }

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = type,
            AgentName = _runtimeCoordinator.CurrentAgentName,
            Message = tool.Name,
            Data = payload
        }, ct);
    }

    private static Dictionary<string, object> BuildDecisionMetadata(ToolExecutionDecision decision)
    {
        var metadata = new Dictionary<string, object>
        {
            ["requiresApproval"] = decision.RequiresApproval,
            ["riskLevel"] = decision.Policy.RiskLevel.ToString(),
            ["timeoutMs"] = decision.Policy.Timeout.TotalMilliseconds,
            ["maxRetries"] = decision.Policy.MaxRetries
        };

        if (decision.ApprovalRequest is not null)
        {
            metadata["approvalId"] = decision.ApprovalRequest.Id;
        }

        return metadata;
    }

    private class DummyDbTool : ITool
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public ToolCategory Category { get; }
        public bool RequiresAuth { get; }

        public DummyDbTool(DbToolEntity entity)
        {
            Id = entity.Id;
            Name = entity.Name;
            Description = entity.Description;
            Category = Enum.TryParse<ToolCategory>(entity.Category, true, out var cat) ? cat : ToolCategory.Api;
            RequiresAuth = entity.RequiresAuth;
        }

        public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
        {
            return Task.FromResult(ToolResult.Fail("This is a placeholder tool for database tracking. Real execution requires full code binding."));
        }

        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(false);
    }
}
