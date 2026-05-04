using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Api.Controllers;

/// <summary>
/// Admin API para monitoramento e gestão do Gateway de serviços.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/gateway")]
public class GatewayController : ControllerBase
{
    private readonly IServiceGateway _gateway;
    private readonly ILogger<GatewayController> _logger;

    public GatewayController(IServiceGateway gateway, ILogger<GatewayController> logger)
    {
        _gateway = gateway;
        _logger = logger;
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
    public async Task<IActionResult> EnableService(string name)
    {
        await _gateway.EnableServiceAsync(name);
        _logger.LogInformation("✅ Service enabled via API: {Service}", name);
        return NoContent();
    }

    /// <summary>
    /// Desabilitar um serviço.
    /// </summary>
    [HttpPost("services/{name}/disable")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DisableService(string name)
    {
        await _gateway.DisableServiceAsync(name);
        _logger.LogWarning("⛔ Service disabled via API: {Service}", name);
        return NoContent();
    }
}
