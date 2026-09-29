using System.Globalization;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Infrastructure.Tools;

public sealed class BannerProductionTool : ITool
{
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IWorkflowStore _workflowStore;
    private readonly IWorkflowEngine _workflowEngine;

    public BannerProductionTool(
        ITenantContextAccessor tenantAccessor,
        IWorkflowStore workflowStore,
        IWorkflowEngine workflowEngine)
    {
        _tenantAccessor = tenantAccessor ?? throw new ArgumentNullException(nameof(tenantAccessor));
        _workflowStore = workflowStore ?? throw new ArgumentNullException(nameof(workflowStore));
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
    }

    public string Id => "banner-production";
    public string Name => "Banner Production";
    public string Description => "Inicia o workflow tenant-scoped de produção de banners.";
    public ToolCategory Category => ToolCategory.AI;
    public bool RequiresAuth => true;

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        if (!string.Equals(input.Action, "generate", StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail("Action not supported. Use 'generate'.");

        if (!TryGetString(input.Parameters, "imagePath", out var imagePath))
            return ToolResult.Fail("Parameter 'imagePath' is required.");
        if (!TryGetValue(input.Parameters, "price", out var priceValue) || !TryReadDecimal(priceValue, out var price))
            return ToolResult.Fail("Parameter 'price' is required and must be a number.");
        if (!TryGetValue(input.Parameters, "bedrooms", out var bedroomsValue) || !TryReadInteger(bedroomsValue, out var bedrooms))
            return ToolResult.Fail("Parameter 'bedrooms' is required and must be an integer.");
        if (string.IsNullOrWhiteSpace(input.UserId))
            return ToolResult.Fail("An authenticated user is required to start a workflow.");

        string tenantId;
        try
        {
            tenantId = _tenantAccessor.CurrentTenantId;
        }
        catch (InvalidOperationException)
        {
            return ToolResult.Fail("A tenant context is required to start a workflow.");
        }

        try
        {
            var definition = await _workflowStore.GetDefinitionAsync(tenantId, "banner-production", ct);
            if (definition is null)
            {
                var tenantDefinitions = await _workflowStore.ListDefinitionsAsync(tenantId, limit: 100, ct);
                definition = tenantDefinitions.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, "Banner Production Workflow", StringComparison.OrdinalIgnoreCase));
            }

            if (definition is null)
                return ToolResult.Fail("The banner-production workflow is not configured for the active tenant.");

            WorkflowGraphValidator.Validate(definition);
            var execution = await _workflowEngine.StartAsync(
                tenantId,
                definition,
                new Dictionary<string, object>
                {
                    ["imagePath"] = imagePath,
                    ["price"] = price,
                    ["bedrooms"] = bedrooms
                },
                input.UserId,
                ct);

            var statusUrl = $"/api/workflow/executions/{Uri.EscapeDataString(execution.Id)}";
            return ToolResult.Ok(
                $"Workflow iniciado. executionId={execution.Id}; status={execution.Status}; statusUrl={statusUrl}",
                new Dictionary<string, object>
                {
                    ["id"] = execution.Id,
                    ["executionId"] = execution.Id,
                    ["workflowId"] = execution.WorkflowId,
                    ["status"] = execution.Status.ToString(),
                    ["statusUrl"] = statusUrl
                });
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Workflow failed: {ex.Message}");
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    private static bool TryGetString(IReadOnlyDictionary<string, object> parameters, string key, out string value)
    {
        value = string.Empty;
        if (!TryGetValue(parameters, key, out var raw) || raw is null)
            return false;

        value = raw is JsonElement { ValueKind: JsonValueKind.String } json ? json.GetString() ?? string.Empty : raw.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object> parameters, string key, out object? value)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            value = raw;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadDecimal(object? value, out decimal result)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Number } json)
            return json.TryGetDecimal(out result);
        var text = value?.ToString();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryReadInteger(object? value, out int result)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Number } json)
            return json.TryGetInt32(out result);
        return int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }
}
