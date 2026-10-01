using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Api.Auth;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Api.Hubs;
using AgenticSystem.Infrastructure.Persistence;

namespace AgenticSystem.Api.Controllers;

/// <summary>
/// Admin API para monitoramento e gestão do Gateway de serviços.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/gateway")]
public class GatewayController : ControllerBase, IAsyncActionFilter
{
    private readonly IServiceGateway _gateway;
    private readonly ILogger<GatewayController> _logger;
    private readonly IHubContext<GatewayHub> _hubContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ISystemOperationContextAccessor _systemOperations;
    private readonly AgenticDbContext _dbContext;

    public GatewayController(
        IServiceGateway gateway,
        ILogger<GatewayController> logger,
        IHubContext<GatewayHub> hubContext,
        ITenantContextAccessor tenantContextAccessor,
        ISystemOperationContextAccessor systemOperations,
        AgenticDbContext dbContext)
    {
        _gateway = gateway;
        _logger = logger;
        _hubContext = hubContext;
        _tenantContextAccessor = tenantContextAccessor;
        _systemOperations = systemOperations;
        _dbContext = dbContext;
    }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!await PlatformAdminAuthorization.IsPlatformAdministratorAsync(
                _dbContext, _systemOperations, context.HttpContext.User, context.HttpContext.RequestAborted))
        {
            context.Result = Forbid();
            return;
        }

        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformGatewayOperation);
        _systemOperations.Require(SystemOperationKind.PlatformGatewayOperation);
        await next();
    }

    /// <summary>
    /// Dashboard consolidado — health, costs, métricas.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = await _gateway.GetDashboardAsync();
        return Ok(dashboard);
    }

    /// <summary>
    /// Status de todos os serviços registrados.
    /// </summary>
    [HttpGet("services")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetServices()
    {
        var services = await _gateway.GetAllServicesStatusAsync();
        return Ok(services);
    }

    /// <summary>
    /// Status de um serviço específico.
    /// </summary>
    [HttpGet("services/{name}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetService(string name)
    {
        try
        {
            var status = await _gateway.GetServiceStatusAsync(name);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Service '{name}' not found" });
        }
    }

    /// <summary>
    /// Serviços filtrados por categoria.
    /// </summary>
    [HttpGet("services/category/{category}")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetServicesByCategory(string category)
    {
        var services = await _gateway.GetServicesByCategoryAsync(category);
        return Ok(services);
    }

    /// <summary>
    /// Relatório de custos.
    /// </summary>
    [HttpGet("costs")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetCosts()
    {
        var costs = await _gateway.GetCostReportAsync();
        return Ok(costs);
    }

    /// <summary>
    /// Relatório de saúde.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetHealth()
    {
        var health = await _gateway.GetHealthReportAsync();
        return Ok(health);
    }

    /// <summary>
    /// Habilitar um serviço.
    /// </summary>
    [HttpPost("services/{name}/enable")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EnableService(string name)
    {
        try
        {
            await _gateway.EnableServiceAsync(name);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Service '{name}' not found" });
        }
        _logger.LogInformation("✅ Service enabled via API: {Service}", name);
        await BroadcastServiceStatusAsync(name, enabled: true);
        return NoContent();
    }

    /// <summary>
    /// Desabilitar um serviço.
    /// </summary>
    [HttpPost("services/{name}/disable")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DisableService(string name)
    {
        try
        {
            await _gateway.DisableServiceAsync(name);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Service '{name}' not found" });
        }
        _logger.LogWarning("⛔ Service disabled via API: {Service}", name);
        await BroadcastServiceStatusAsync(name, enabled: false);
        return NoContent();
    }

    private Task BroadcastServiceStatusAsync(string serviceName, bool enabled) =>
        _hubContext.Clients.Group($"tenant:{_tenantContextAccessor.CurrentTenantId}:gateway")
            .SendAsync("ServiceStatusChanged", new
            {
                serviceName,
                enabled,
                tenantId = _tenantContextAccessor.CurrentTenantId,
                timestamp = DateTime.UtcNow
            });
}
