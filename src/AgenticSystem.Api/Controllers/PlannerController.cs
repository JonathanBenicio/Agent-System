using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Infrastructure.AI;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PlannerController : ControllerBase
{
    private readonly ChatClientPlanner _planner;
    private readonly ILogger<PlannerController> _logger;

    public PlannerController(
        ChatClientPlanner planner,
        ILogger<PlannerController> logger)
    {
        _planner = planner;
        _logger = logger;
    }

    /// <summary>
    /// Gera um plano de execução para um objetivo do usuário.
    /// </summary>
    [HttpPost("plan")]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreatePlanRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new { error = "UserId é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.Objective))
            return BadRequest(new { error = "Objective é obrigatório." });

        _logger.LogInformation("📋 Gerando plano para {UserId}: {Objective}",
            request.UserId, request.Objective[..Math.Min(80, request.Objective.Length)]);

        var plan = await _planner.PlanAsync(request.UserId, request.Objective, ct);

        if (plan == null)
        {
            _logger.LogWarning("⚠️ Planner retornou null para {UserId}", request.UserId);
            return UnprocessableEntity(new { error = "Não foi possível gerar um plano para o objetivo informado." });
        }

        _logger.LogInformation("✅ Plano gerado: {Steps} steps", plan.Steps?.Count ?? 0);
        return Ok(plan);
    }
}

public class CreatePlanRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
}
