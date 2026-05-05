using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SetupController : ControllerBase
{
    private readonly ISetupFlowManager _setupFlowManager;
    private readonly ILogger<SetupController> _logger;

    public SetupController(
        ISetupFlowManager setupFlowManager,
        ILogger<SetupController> logger)
    {
        _setupFlowManager = setupFlowManager;
        _logger = logger;
    }

    /// <summary>
    /// Inicia o flow de onboarding para um usuário.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartSetup([FromBody] StartSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new { error = "UserId é obrigatório." });

        var state = await _setupFlowManager.StartSetupAsync(request.UserId);
        _logger.LogInformation("🚀 Setup iniciado para usuário {UserId}", request.UserId);
        return Ok(state);
    }

    /// <summary>
    /// Processa a resposta do usuário ao step atual do setup.
    /// </summary>
    [HttpPost("step")]
    public async Task<IActionResult> ProcessStep([FromBody] ProcessStepRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new { error = "UserId é obrigatório." });

        var isInFlow = await _setupFlowManager.IsInSetupFlowAsync(request.UserId);
        if (!isInFlow)
            return NotFound(new { error = $"Nenhum flow de setup ativo para o usuário '{request.UserId}'." });

        var state = await _setupFlowManager.ProcessStepResponseAsync(request.UserId, request.Response);
        return Ok(state);
    }

    /// <summary>
    /// Obtém o estado atual do flow de setup.
    /// </summary>
    [HttpGet("state/{userId}")]
    public async Task<IActionResult> GetSetupState(string userId)
    {
        var state = await _setupFlowManager.GetSetupStateAsync(userId);
        if (state == null)
            return NotFound(new { error = $"Nenhum flow de setup encontrado para '{userId}'." });

        return Ok(state);
    }

    /// <summary>
    /// Verifica se o usuário está em um flow de setup ativo.
    /// </summary>
    [HttpGet("active/{userId}")]
    public async Task<IActionResult> IsInSetupFlow(string userId)
    {
        var isActive = await _setupFlowManager.IsInSetupFlowAsync(userId);
        return Ok(new { userId, isActive });
    }
}

public class StartSetupRequest
{
    public string UserId { get; set; } = string.Empty;
}

public class ProcessStepRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
}
