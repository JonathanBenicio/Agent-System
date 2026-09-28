using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticSystem.Infrastructure.Tools;

public class BannerProductionTool : ITool
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantContextAccessor _tenantAccessor;

    public BannerProductionTool(IServiceScopeFactory scopeFactory, ITenantContextAccessor tenantAccessor)
    {
        _scopeFactory = scopeFactory;
        _tenantAccessor = tenantAccessor;
    }

    public string Id => "banner-production";
    public string Name => "Banner Production";
    public string Description => "Orquestra a criacao de banners imobiliarios usando MAF Workflow";
    public ToolCategory Category => ToolCategory.AI;
    public bool RequiresAuth => false;

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        if (input.Action != "generate")
            return ToolResult.Fail("Action not supported. Use 'generate'.");

        if (!input.Parameters.TryGetValue("imagePath", out var imagePathObj) || string.IsNullOrWhiteSpace(imagePathObj?.ToString()))
            return ToolResult.Fail("Parameter 'imagePath' is required.");

        var imagePath = imagePathObj is System.Text.Json.JsonElement jsonEl
            ? jsonEl.GetString() ?? string.Empty
            : imagePathObj.ToString() ?? string.Empty;

        if (!input.Parameters.TryGetValue("price", out var priceObj) || !decimal.TryParse(priceObj.ToString(), out var price))
            return ToolResult.Fail("Parameter 'price' is required and must be a number.");

        if (!input.Parameters.TryGetValue("bedrooms", out var bedObj) || !int.TryParse(bedObj.ToString(), out var bedrooms))
            return ToolResult.Fail("Parameter 'bedrooms' is required and must be an integer.");

        try
        {
            string? activeTenantId = null;
            try { activeTenantId = _tenantAccessor.CurrentTenantId; } catch (InvalidOperationException) { }
            
            var parameters = new Dictionary<string, object>
            {
                { "imagePath", imagePath },
                { "price", price },
                { "bedrooms", bedrooms }
            };

            await using var scope = _scopeFactory.CreateAsyncScope();
            var workflowCompiler = scope.ServiceProvider.GetRequiredService<IDynamicWorkflowCompiler>();
            var result = await workflowCompiler.ExecuteDynamicWorkflowAsync("banner-production", activeTenantId ?? "admin", parameters, ct);

            if (result.IsAsync)
            {
                // Async HTTP API pattern: workflow runs in background.
                // Return RunId so the caller can poll for completion.
                return ToolResult.Ok($"Workflow iniciado em background. RunId={result.RunId} — acompanhe via GET /api/workflow/executions/{result.RunId}");
            }

            return ToolResult.Ok(result.Message);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Workflow failed: {ex.ToString()}");
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }
}
