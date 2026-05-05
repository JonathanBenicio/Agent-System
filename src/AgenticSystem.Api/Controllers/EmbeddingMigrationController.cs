using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/embedding-migration")]
public class EmbeddingMigrationController : ControllerBase
{
    private readonly IEmbeddingMigrationManager _migrationManager;
    private readonly IEmbeddingModelStore _modelStore;
    private readonly ILogger<EmbeddingMigrationController> _logger;

    public EmbeddingMigrationController(
        IEmbeddingMigrationManager migrationManager,
        IEmbeddingModelStore modelStore,
        ILogger<EmbeddingMigrationController> logger)
    {
        _migrationManager = migrationManager;
        _modelStore = modelStore;
        _logger = logger;
    }

    // ─── Models ───────────────────────────────────────────

    [HttpGet("models")]
    public async Task<IActionResult> GetModels()
    {
        var models = await _modelStore.GetAllAsync();
        return Ok(models.Select(MaskApiKey));
    }

    [HttpGet("models/{modelId}")]
    public async Task<IActionResult> GetModel(string modelId)
    {
        var model = await _modelStore.GetAsync(modelId);
        if (model == null) return NotFound();
        return Ok(MaskApiKey(model));
    }

    [HttpGet("models/active")]
    public async Task<IActionResult> GetActiveModel()
    {
        try
        {
            var model = await _modelStore.GetActiveAsync();
            return Ok(MaskApiKey(model));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("models")]
    public async Task<IActionResult> SaveModel([FromBody] EmbeddingModelConfig model)
    {
        await _modelStore.SaveAsync(model);
        return Ok(MaskApiKey(model));
    }

    [HttpDelete("models/{modelId}")]
    public async Task<IActionResult> DeleteModel(string modelId)
    {
        await _modelStore.DeleteAsync(modelId);
        return NoContent();
    }

    [HttpPost("models/{modelId}/activate")]
    public async Task<IActionResult> ActivateModel(string modelId)
    {
        try
        {
            await _modelStore.SetActiveAsync(modelId);
            return Ok(new { message = $"Model '{modelId}' activated" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Model '{modelId}' not found" });
        }
    }

    // ─── Migration Jobs ──────────────────────────────────

    [HttpPost("jobs")]
    public async Task<IActionResult> StartMigration([FromBody] StartMigrationRequest request)
    {
        try
        {
            var job = await _migrationManager.StartMigrationAsync(request);
            return CreatedAtAction(nameof(GetJob), new { jobId = job.Id }, job);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetAllJobs()
    {
        var jobs = await _migrationManager.GetAllJobsAsync();
        return Ok(jobs);
    }

    [HttpGet("jobs/{jobId}")]
    public async Task<IActionResult> GetJob(string jobId)
    {
        var job = await _migrationManager.GetJobAsync(jobId);
        if (job == null) return NotFound();
        return Ok(job);
    }

    [HttpGet("jobs/{jobId}/status")]
    public async Task<IActionResult> GetJobStatus(string jobId)
    {
        try
        {
            var status = await _migrationManager.GetStatusAsync(jobId);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Job '{jobId}' not found" });
        }
    }

    [HttpPost("jobs/{jobId}/cancel")]
    public async Task<IActionResult> CancelJob(string jobId)
    {
        try
        {
            await _migrationManager.CancelAsync(jobId);
            return Ok(new { message = "Job cancelled" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Job '{jobId}' not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("jobs/{jobId}/retry")]
    public async Task<IActionResult> RetryJob(string jobId)
    {
        try
        {
            await _migrationManager.RetryAsync(jobId);
            return Ok(new { message = "Job retried" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Job '{jobId}' not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("jobs/{jobId}/switch")]
    public async Task<IActionResult> SwitchCollection(string jobId)
    {
        try
        {
            await _migrationManager.SwitchCollectionAsync(jobId);
            return Ok(new { message = "Collection switched to target" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Job '{jobId}' not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static object MaskApiKey(EmbeddingModelConfig model) => new
    {
        model.Id,
        model.Name,
        model.Provider,
        model.ModelName,
        model.Dimensions,
        model.BaseUrl,
        ApiKey = string.IsNullOrEmpty(model.ApiKey) ? null : "********",
        model.IsActive,
        model.CreatedAt
    };
}
