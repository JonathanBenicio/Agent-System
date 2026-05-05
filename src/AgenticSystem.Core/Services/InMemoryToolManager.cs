using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

public class InMemoryToolManager : IToolManager
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ToolRegistration>> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IToolGovernanceService? _toolGovernance;
    private readonly IAgentRuntimeCoordinator? _runtimeCoordinator;
    private readonly ILogger<InMemoryToolManager> _logger;

    public InMemoryToolManager(
        ILogger<InMemoryToolManager> logger,
        IToolGovernanceService? toolGovernance = null,
        IAgentRuntimeCoordinator? runtimeCoordinator = null)
    {
        _logger = logger;
        _toolGovernance = toolGovernance;
        _runtimeCoordinator = runtimeCoordinator;
    }

    public async Task<ToolResult> ExecuteToolAsync(string toolId, ToolInput input, CancellationToken ct = default)
    {
        var registration = ResolveRegistration(toolId, input);
        var tool = registration?.Tool;

        if (tool is null && !_tools.TryGetValue(toolId, out tool))
        {
            _logger.LogWarning("🔧 Tool não encontrada: {ToolId}", toolId);
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

        var cacheKey = BuildCacheKey(toolId, tool!, input, registration);
        if (decision.Policy.EnableCache
            && _cache.TryGetValue(cacheKey, out var cached)
            && cached.ExpiresAt > DateTime.UtcNow)
        {
            await PublishToolEventAsync(AgentStreamEventType.ToolCompleted, tool, input.Action, 0, true, new Dictionary<string, object>
            {
                ["cacheHit"] = true
            }, ct);

            return ToolResult.Ok(cached.Result.Data, new Dictionary<string, object>
            {
                ["cacheHit"] = true,
                ["toolId"] = tool.Id,
                ["logicalToolId"] = toolId,
                ["toolVersion"] = registration?.Version ?? "1.0.0",
                ["toolVariant"] = registration?.VariantName ?? "default"
            });
        }

        _logger.LogInformation("🔧 Executando tool: {ToolId} | Action: {Action}", toolId, input.Action);
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

                if (decision.Policy.EnableCache && result.Success)
                {
                    _cache[cacheKey] = new CacheEntry(result, DateTime.UtcNow.Add(decision.Policy.CacheTtl));
                }

                await _runtimeCoordinator?.RecordArtifactAsync(new AgentExecutionArtifact
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
                }, ct)!;

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

    public Task<IEnumerable<ITool>> GetAvailableToolsAsync(string? category = null)
    {
        IEnumerable<ITool> tools = _tools.Values;

        if (_registrations.Count > 0)
        {
            tools = _registrations.Keys
                .Select(ResolveDefaultRegistration)
                .Where(registration => registration is not null)
                .Select(registration => registration!.Tool)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ToolCategory>(category, true, out var cat))
        {
            tools = tools.Where(t => t.Category == cat);
        }

        return Task.FromResult(tools);
    }

    public void RegisterTool(ITool tool)
    {
        _tools[tool.Id] = tool;
        RegisterToolVariant(tool.Id, tool, version: "1.0.0", isDefault: true);
        _logger.LogInformation("🔧 Tool registrada: {ToolName} ({Category})", tool.Name, tool.Category);
    }

    public void RegisterToolVariant(
        string logicalToolId,
        ITool tool,
        string version,
        string? variantName = null,
        int rolloutPercentage = 100,
        bool isDefault = false)
    {
        var registrations = _registrations.GetOrAdd(
            logicalToolId,
            _ => new ConcurrentDictionary<string, ToolRegistration>(StringComparer.OrdinalIgnoreCase));

        var registration = new ToolRegistration
        {
            LogicalToolId = logicalToolId,
            Tool = tool,
            Version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version,
            VariantName = variantName,
            RolloutPercentage = Math.Clamp(rolloutPercentage, 0, 100),
            IsDefault = isDefault
        };

        registrations[registration.RegistrationKey] = registration;

        if (isDefault || registrations.Count == 1)
        {
            _tools[logicalToolId] = tool;
            foreach (var existing in registrations.Values.Where(existing => existing.RegistrationKey != registration.RegistrationKey))
            {
                existing.IsDefault = false;
            }

            registration.IsDefault = true;
        }

        _logger.LogInformation(
            "🔧 Tool variant registered: {LogicalToolId} v{Version} ({Variant}) rollout={RolloutPercentage}% default={IsDefault}",
            logicalToolId,
            registration.Version,
            registration.VariantName ?? "default",
            registration.RolloutPercentage,
            registration.IsDefault);
    }

    public Task<IReadOnlyList<ToolRegistration>> GetRegistrationsAsync(string logicalToolId, CancellationToken ct = default)
    {
        if (!_registrations.TryGetValue(logicalToolId, out var registrations))
        {
            return Task.FromResult<IReadOnlyList<ToolRegistration>>([]);
        }

        var result = registrations.Values
            .OrderByDescending(registration => registration.IsDefault)
            .ThenBy(registration => registration.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(registration => registration.VariantName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<ToolRegistration>>(result);
    }

    public bool UnregisterTool(string toolId)
    {
        var removed = _tools.TryRemove(toolId, out _);
        _registrations.TryRemove(toolId, out _);
        if (removed)
            _logger.LogInformation("🔧 Tool removida: {ToolId}", toolId);
        return removed;
    }

    public ITool? GetTool(string toolId)
    {
        var registration = ResolveDefaultRegistration(toolId);
        if (registration is not null)
        {
            return registration.Tool;
        }

        _tools.TryGetValue(toolId, out var tool);
        return tool;
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
        if (_runtimeCoordinator is null)
        {
            return;
        }

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

    private ToolRegistration? ResolveRegistration(string logicalToolId, ToolInput input)
    {
        if (!_registrations.TryGetValue(logicalToolId, out var registrations) || registrations.Count == 0)
        {
            return null;
        }

        if (input.Parameters.TryGetValue("toolVersion", out var requestedVersion) && requestedVersion is not null)
        {
            var matchByVersion = registrations.Values.FirstOrDefault(registration =>
                registration.Version.Equals(requestedVersion.ToString(), StringComparison.OrdinalIgnoreCase));
            if (matchByVersion is not null)
            {
                return matchByVersion;
            }
        }

        if (input.Parameters.TryGetValue("toolVariant", out var requestedVariant) && requestedVariant is not null)
        {
            var matchByVariant = registrations.Values.FirstOrDefault(registration =>
                string.Equals(registration.VariantName, requestedVariant.ToString(), StringComparison.OrdinalIgnoreCase));
            if (matchByVariant is not null)
            {
                return matchByVariant;
            }
        }

        var defaultRegistration = registrations.Values.FirstOrDefault(registration => registration.IsDefault)
            ?? registrations.Values.OrderByDescending(registration => registration.RegisteredAt).First();

        var experimentalRegistrations = registrations.Values
            .Where(registration => !registration.IsDefault && registration.RolloutPercentage > 0)
            .OrderBy(registration => registration.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(registration => registration.VariantName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (experimentalRegistrations.Count == 0)
        {
            return defaultRegistration;
        }

        var identity = input.UserId
            ?? _runtimeCoordinator?.CurrentSessionId
            ?? _runtimeCoordinator?.CurrentAgentName
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(identity))
        {
            return defaultRegistration;
        }

        var bucket = ComputeDeterministicBucket(logicalToolId, identity);
        var cumulative = 0;
        foreach (var registration in experimentalRegistrations)
        {
            cumulative += registration.RolloutPercentage;
            if (bucket < cumulative)
            {
                return registration;
            }
        }

        return defaultRegistration;
    }

    private ToolRegistration? ResolveDefaultRegistration(string logicalToolId)
    {
        if (_registrations.TryGetValue(logicalToolId, out var registrations) && registrations.Count > 0)
        {
            return registrations.Values.FirstOrDefault(registration => registration.IsDefault)
                ?? registrations.Values.OrderByDescending(registration => registration.RegisteredAt).First();
        }

        return null;
    }

    private static int ComputeDeterministicBucket(string toolId, string identity)
    {
        var raw = $"{toolId}:{identity}";
        var hash = Math.Abs(raw.GetHashCode(StringComparison.OrdinalIgnoreCase));
        return hash % 100;
    }

    private static string BuildCacheKey(string logicalToolId, ITool tool, ToolInput input, ToolRegistration? registration)
        => $"{logicalToolId}:{tool.Id}:{registration?.Version ?? "1.0.0"}:{registration?.VariantName ?? "default"}:{input.Action}:{System.Text.Json.JsonSerializer.Serialize(input.Parameters)}";

    private sealed record CacheEntry(ToolResult Result, DateTime ExpiresAt);
}
