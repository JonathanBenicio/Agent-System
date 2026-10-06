using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/golden-sets")]
[Route("api/evaluation/golden-sets")]
[Route("api/goldensets")]
public class GoldenSetController : ControllerBase
{
    private readonly IGoldenSetRepository _repository;
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentEvaluationService _evaluationService;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GoldenSetController> _logger;

    public GoldenSetController(
        IGoldenSetRepository repository,
        IAgentFactory agentFactory,
        IAgentEvaluationService evaluationService,
        ITenantContextAccessor tenantContextAccessor,
        IMemoryCache memoryCache,
        IServiceProvider serviceProvider,
        ILogger<GoldenSetController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? agentName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var list = await _repository.ListAsync(tenantId, agentName, page, pageSize, ct);

        var dtos = list.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var model = await _repository.GetByIdAsync(id, tenantId, ct);
        if (model == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(model));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoldenSetDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenantId = _tenantContextAccessor.CurrentTenantId;

        // Custom validation: Agent must exist
        var agents = await _agentFactory.GetAllAgentsAsync();
        if (!agents.Any(a => a.Name.Equals(dto.AgentName, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest($"Agent '{dto.AgentName}' is not registered in the system catalog.");
        }

        // Custom validation: Cases count limits
        if (dto.Cases == null || dto.Cases.Count == 0)
        {
            return BadRequest("A golden set must contain at least one test case.");
        }

        if (dto.Cases.Count > 500)
        {
            return BadRequest("A golden set cannot contain more than 500 test cases.");
        }

        if (dto.Cases.Any(c => c == null || string.IsNullOrWhiteSpace(c.Input)))
        {
            return BadRequest("All test cases must have a valid non-empty input.");
        }

        var model = new GoldenSet
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            AgentName = dto.AgentName,
            Cases = dto.Cases.Select(c => new GoldenSetCase
            {
                Id = string.IsNullOrWhiteSpace(c.Id) ? Guid.NewGuid().ToString("N") : c.Id,
                Input = c.Input,
                ExpectedOutput = c.ExpectedOutput,
                Tags = c.Tags ?? new()
            }).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(model, ct);
        _logger.LogInformation("Created golden set {Name} (ID: {Id}) for tenant {TenantId}", model.Name, model.Id, tenantId);

        return CreatedAtAction(nameof(GetById), new { id = model.Id }, MapToDto(model));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateGoldenSetDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var model = await _repository.GetByIdAsync(id, tenantId, ct);
        if (model == null)
        {
            return NotFound();
        }

        // Custom validation: Agent must exist
        var agents = await _agentFactory.GetAllAgentsAsync();
        if (!agents.Any(a => a.Name.Equals(dto.AgentName, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest($"Agent '{dto.AgentName}' is not registered in the system catalog.");
        }

        // Custom validation: Cases count limits
        if (dto.Cases == null || dto.Cases.Count == 0)
        {
            return BadRequest("A golden set must contain at least one test case.");
        }

        if (dto.Cases.Count > 500)
        {
            return BadRequest("A golden set cannot contain more than 500 test cases.");
        }

        if (dto.Cases.Any(c => c == null || string.IsNullOrWhiteSpace(c.Input)))
        {
            return BadRequest("All test cases must have a valid non-empty input.");
        }

        model.Name = dto.Name;
        model.Description = dto.Description;
        model.AgentName = dto.AgentName;
        model.Cases = dto.Cases.Select(c => new GoldenSetCase
        {
            Id = string.IsNullOrWhiteSpace(c.Id) ? Guid.NewGuid().ToString("N") : c.Id,
            Input = c.Input,
            ExpectedOutput = c.ExpectedOutput,
            Tags = c.Tags ?? new()
        }).ToList();
        model.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(model, ct);
        _logger.LogInformation("Updated golden set {Name} (ID: {Id}) for tenant {TenantId}", model.Name, model.Id, tenantId);

        return Ok(MapToDto(model));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var model = await _repository.GetByIdAsync(id, tenantId, ct);
        if (model == null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id, tenantId, ct);
        _logger.LogInformation("Deleted golden set ID: {Id} for tenant {TenantId}", id, tenantId);

        return NoContent();
    }

    [HttpPost("{id}/run")]
    public async Task<IActionResult> Run(string id, CancellationToken ct = default)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var model = await _repository.GetByIdAsync(id, tenantId, ct);
        if (model == null)
        {
            return NotFound();
        }

        _logger.LogInformation("Running evaluation for golden set {Name} (ID: {Id})", model.Name, model.Id);

        // Convert GoldenSet cases to EvalTestCases
        var evalTestCases = model.Cases.Select(c => new EvalTestCase
        {
            Id = c.Id,
            Name = $"Case_{c.Id}",
            AgentName = model.AgentName,
            Input = c.Input,
            ExpectedOutput = c.ExpectedOutput,
            ExpectedKeywords = string.IsNullOrWhiteSpace(c.ExpectedOutput)
                ? new List<string>()
                : new List<string> { c.ExpectedOutput },
            Tags = c.Tags ?? new()
        }).ToList();

        if (evalTestCases.Count <= 20)
        {
            // Run evaluation suite using IAgentEvaluationService
            var suiteResult = await _evaluationService.RunSuiteAsync(evalTestCases, agentVersionId: null, ct);

            // Map EvalSuiteResult to GoldenSetRunResultDto
            var runResult = new GoldenSetRunResultDto
            {
                SuiteId = suiteResult.SuiteId,
                AgentName = suiteResult.AgentName,
                AgentVersionId = suiteResult.AgentVersionId,
                TotalTests = suiteResult.TotalTests,
                Passed = suiteResult.Passed,
                Failed = suiteResult.Failed,
                OverallScore = suiteResult.OverallScore,
                AccuracyScore = suiteResult.AccuracyScore,
                SafetyScore = suiteResult.SafetyScore,
                LatencyP50Ms = suiteResult.LatencyP50Ms,
                LatencyP95Ms = suiteResult.LatencyP95Ms,
                TotalTokensUsed = suiteResult.TotalTokensUsed,
                Results = suiteResult.Results,
                Regressions = suiteResult.Regressions,
                StartedAt = suiteResult.StartedAt,
                CompletedAt = suiteResult.CompletedAt
            };

            return Ok(runResult);
        }
        else
        {
            var runId = Guid.NewGuid().ToString();
            var cacheKey = ("golden-set-run", tenantId, runId);
            var status = new GoldenSetRunStatusDto
            {
                RunId = runId,
                GoldenSetId = id,
                Status = "Running",
                TotalTests = evalTestCases.Count,
                CompletedTests = 0
            };

            _memoryCache.Set(cacheKey, status, TimeSpan.FromMinutes(30));

            // Execute evaluation in background using Task.Run
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var scopedAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
                        using (scopedAccessor.BeginScope(new TenantContext { TenantId = tenantId }))
                        {
                            var scopedEvalService = scope.ServiceProvider.GetRequiredService<IAgentEvaluationService>();
                            var suiteResult = await scopedEvalService.RunSuiteAsync(evalTestCases, agentVersionId: null, CancellationToken.None);

                            var runResult = new GoldenSetRunResultDto
                            {
                                SuiteId = suiteResult.SuiteId,
                                AgentName = suiteResult.AgentName,
                                AgentVersionId = suiteResult.AgentVersionId,
                                TotalTests = suiteResult.TotalTests,
                                Passed = suiteResult.Passed,
                                Failed = suiteResult.Failed,
                                OverallScore = suiteResult.OverallScore,
                                AccuracyScore = suiteResult.AccuracyScore,
                                SafetyScore = suiteResult.SafetyScore,
                                LatencyP50Ms = suiteResult.LatencyP50Ms,
                                LatencyP95Ms = suiteResult.LatencyP95Ms,
                                TotalTokensUsed = suiteResult.TotalTokensUsed,
                                Results = suiteResult.Results,
                                Regressions = suiteResult.Regressions,
                                StartedAt = suiteResult.StartedAt,
                                CompletedAt = suiteResult.CompletedAt
                            };

                            if (_memoryCache.TryGetValue<GoldenSetRunStatusDto>(cacheKey, out var currentStatus) && currentStatus is not null)
                            {
                                currentStatus.Status = "Completed";
                                currentStatus.CompletedTests = suiteResult.TotalTests;
                                currentStatus.Result = runResult;
                                _memoryCache.Set(cacheKey, currentStatus, TimeSpan.FromMinutes(30));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_memoryCache.TryGetValue<GoldenSetRunStatusDto>(cacheKey, out var currentStatus) && currentStatus is not null)
                    {
                        currentStatus.Status = "Failed";
                        currentStatus.ErrorMessage = ex.Message;
                        _memoryCache.Set(cacheKey, currentStatus, TimeSpan.FromMinutes(30));
                    }
                }
            });

            return Accepted(new
            {
                runId = runId,
                status = "Running",
                message = "Evaluation started asynchronously."
            });
        }
    }

    [HttpGet("runs/{runId}")]
    public IActionResult GetRunStatus(string runId)
    {
        var cacheKey = ("golden-set-run", _tenantContextAccessor.CurrentTenantId, runId);
        if (_memoryCache.TryGetValue<GoldenSetRunStatusDto>(cacheKey, out var status))
        {
            return Ok(status);
        }

        return NotFound();
    }

    private static GoldenSetDto MapToDto(GoldenSet model)
    {
        return new GoldenSetDto
        {
            Id = model.Id,
            TenantId = model.TenantId,
            Name = model.Name,
            Description = model.Description,
            AgentName = model.AgentName,
            Cases = model.Cases.Select(c => new GoldenSetCaseDto
            {
                Id = c.Id,
                Input = c.Input,
                ExpectedOutput = c.ExpectedOutput,
                Tags = c.Tags ?? new()
            }).ToList(),
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
