using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class GoldenSetControllerTests
{
    private readonly IGoldenSetRepository _repository;
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentEvaluationService _evaluationService;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GoldenSetController> _logger;
    private readonly GoldenSetController _sut;

    public GoldenSetControllerTests()
    {
        _repository = Substitute.For<IGoldenSetRepository>();
        _agentFactory = Substitute.For<IAgentFactory>();
        _evaluationService = Substitute.For<IAgentEvaluationService>();
        _tenantContextAccessor = Substitute.For<ITenantContextAccessor>();
        _memoryCache = Substitute.For<IMemoryCache>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<GoldenSetController>>();

        _tenantContextAccessor.CurrentTenantId.Returns("test-tenant");

        var cacheEntry = Substitute.For<ICacheEntry>();
        _memoryCache.CreateEntry(Arg.Any<object>()).Returns(cacheEntry);

        _sut = new GoldenSetController(
            _repository,
            _agentFactory,
            _evaluationService,
            _tenantContextAccessor,
            _memoryCache,
            _serviceProvider,
            _logger);
    }

    [Fact]
    public async Task List_ReturnsOkWithMappedDtos()
    {
        // Arrange
        var list = new List<GoldenSet>
        {
            new GoldenSet
            {
                Id = "gs_1",
                TenantId = "test-tenant",
                Name = "Set 1",
                AgentName = "agent_1",
                Cases = new List<GoldenSetCase>()
            }
        };

        _repository.ListAsync("test-tenant", null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(list);

        // Act
        var result = await _sut.List(null, 1, 20, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value as IEnumerable<GoldenSetDto>;
        dtos.Should().NotBeNull();
        dtos!.Should().HaveCount(1);
        dtos!.First().Id.Should().Be("gs_1");
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsOk()
    {
        // Arrange
        var goldenSet = new GoldenSet
        {
            Id = "gs_1",
            TenantId = "test-tenant",
            Name = "Set 1",
            AgentName = "agent_1"
        };

        _repository.GetByIdAsync("gs_1", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(goldenSet);

        // Act
        var result = await _sut.GetById("gs_1", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value as GoldenSetDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be("gs_1");
    }

    [Fact]
    public async Task GetById_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _repository.GetByIdAsync("nonexistent", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((GoldenSet?)null);

        // Act
        var result = await _sut.GetById("nonexistent", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_WithValidInput_ReturnsCreated()
    {
        // Arrange
        var dto = new CreateGoldenSetDto
        {
            Name = "New Set",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCaseDto>
            {
                new GoldenSetCaseDto { Input = "Input 1", ExpectedOutput = "Expected 1" }
            }
        };

        _agentFactory.GetAllAgentsAsync().Returns(new List<AgentInfo>
        {
            new AgentInfo { Name = "agent_1" }
        });

        // Act
        var result = await _sut.Create(dto, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var createdDto = createdResult.Value as GoldenSetDto;
        createdDto.Should().NotBeNull();
        createdDto!.Name.Should().Be("New Set");
        await _repository.Received(1).AddAsync(Arg.Is<GoldenSet>(g => g.Name == "New Set" && g.AgentName == "agent_1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithNonExistentAgent_ReturnsBadRequest()
    {
        // Arrange
        var dto = new CreateGoldenSetDto
        {
            Name = "New Set",
            AgentName = "invalid_agent",
            Cases = new List<GoldenSetCaseDto>
            {
                new GoldenSetCaseDto { Input = "Input 1" }
            }
        };

        _agentFactory.GetAllAgentsAsync().Returns(new List<AgentInfo>
        {
            new AgentInfo { Name = "some_other_agent" }
        });

        // Act
        var result = await _sut.Create(dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_WithTooManyCases_ReturnsBadRequest()
    {
        // Arrange
        var cases = Enumerable.Range(1, 501).Select(i => new GoldenSetCaseDto { Input = $"Input {i}" }).ToList();
        var dto = new CreateGoldenSetDto
        {
            Name = "New Set",
            AgentName = "agent_1",
            Cases = cases
        };

        _agentFactory.GetAllAgentsAsync().Returns(new List<AgentInfo>
        {
            new AgentInfo { Name = "agent_1" }
        });

        // Act
        var result = await _sut.Create(dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_WithEmptyInputCase_ReturnsBadRequest()
    {
        // Arrange
        var dto = new CreateGoldenSetDto
        {
            Name = "New Set",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCaseDto>
            {
                new GoldenSetCaseDto { Input = "" }
            }
        };

        _agentFactory.GetAllAgentsAsync().Returns(new List<AgentInfo>
        {
            new AgentInfo { Name = "agent_1" }
        });

        // Act
        var result = await _sut.Create(dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_WithNullCase_ReturnsBadRequest()
    {
        // Arrange
        var dto = new CreateGoldenSetDto
        {
            Name = "New Set",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCaseDto>
            {
                null!
            }
        };

        _agentFactory.GetAllAgentsAsync().Returns(new List<AgentInfo>
        {
            new AgentInfo { Name = "agent_1" }
        });

        // Act
        var result = await _sut.Create(dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_WhenExistsAndValid_ReturnsOk()
    {
        // Arrange
        var existing = new GoldenSet
        {
            Id = "gs_1",
            TenantId = "test-tenant",
            Name = "Old Name",
            AgentName = "agent_1"
        };

        var dto = new UpdateGoldenSetDto
        {
            Name = "Updated Name",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCaseDto>
            {
                new GoldenSetCaseDto { Input = "Updated Input" }
            }
        };

        _repository.GetByIdAsync("gs_1", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(existing);

        _agentFactory.GetAllAgentsAsync().Returns(new List<AgentInfo>
        {
            new AgentInfo { Name = "agent_1" }
        });

        // Act
        var result = await _sut.Update("gs_1", dto, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var updatedDto = okResult.Value as GoldenSetDto;
        updatedDto.Should().NotBeNull();
        updatedDto!.Name.Should().Be("Updated Name");
        await _repository.Received(1).UpdateAsync(Arg.Is<GoldenSet>(g => g.Id == "gs_1" && g.Name == "Updated Name"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        var dto = new UpdateGoldenSetDto
        {
            Name = "Updated Name",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCaseDto>()
        };

        _repository.GetByIdAsync("nonexistent", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((GoldenSet?)null);

        // Act
        var result = await _sut.Update("nonexistent", dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_WithNullCase_ReturnsBadRequest()
    {
        // Arrange
        var existing = new GoldenSet
        {
            Id = "gs_1",
            TenantId = "test-tenant",
            Name = "Old Name",
            AgentName = "agent_1"
        };

        var dto = new UpdateGoldenSetDto
        {
            Name = "Updated Name",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCaseDto>
            {
                null!
            }
        };

        _repository.GetByIdAsync("gs_1", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(existing);

        _agentFactory.GetAllAgentsAsync().Returns(new List<AgentInfo>
        {
            new AgentInfo { Name = "agent_1" }
        });

        // Act
        var result = await _sut.Update("gs_1", dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Delete_WhenExists_ReturnsNoContent()
    {
        // Arrange
        var existing = new GoldenSet
        {
            Id = "gs_1",
            TenantId = "test-tenant"
        };

        _repository.GetByIdAsync("gs_1", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act
        var result = await _sut.Delete("gs_1", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _repository.Received(1).DeleteAsync("gs_1", "test-tenant", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _repository.GetByIdAsync("nonexistent", "test-tenant", Arg.Any<CancellationToken>())
            .Returns((GoldenSet?)null);

        // Act
        var result = await _sut.Delete("nonexistent", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Run_WhenExists_RunsSuiteAndReturnsRunResult()
    {
        // Arrange
        var goldenSet = new GoldenSet
        {
            Id = "gs_1",
            TenantId = "test-tenant",
            Name = "Run Set",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCase>
            {
                new GoldenSetCase { Id = "c1", Input = "Test Input", ExpectedOutput = "Expect Output" }
            }
        };

        _repository.GetByIdAsync("gs_1", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(goldenSet);

        var suiteResult = new EvalSuiteResult
        {
            SuiteId = "suite_abc",
            AgentName = "agent_1",
            TotalTests = 1,
            Passed = 1,
            OverallScore = 0.95,
            CompletedAt = DateTime.UtcNow
        };

        _evaluationService.RunSuiteAsync(Arg.Any<IReadOnlyList<EvalTestCase>>(), null, Arg.Any<CancellationToken>())
            .Returns(suiteResult);

        // Act
        var result = await _sut.Run("gs_1", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var runResult = okResult.Value as GoldenSetRunResultDto;
        runResult.Should().NotBeNull();
        runResult!.SuiteId.Should().Be("suite_abc");
        runResult.AgentName.Should().Be("agent_1");
        runResult.OverallScore.Should().Be(0.95);
    }

    [Fact]
    public void GetRunStatus_WhenRunExists_ReturnsOkWithStatus()
    {
        // Arrange
        var runId = "test-run-123";
        var statusDto = new GoldenSetRunStatusDto
        {
            RunId = runId,
            GoldenSetId = "gs_1",
            Status = "Running",
            TotalTests = 25,
            CompletedTests = 5
        };

        object? cachedVal;
        _memoryCache.TryGetValue(("golden-set-run", "test-tenant", runId), out cachedVal).Returns(x =>
        {
            x[1] = statusDto;
            return true;
        });

        // Act
        var result = _sut.GetRunStatus(runId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedStatus = okResult.Value as GoldenSetRunStatusDto;
        returnedStatus.Should().NotBeNull();
        returnedStatus!.RunId.Should().Be(runId);
        returnedStatus.Status.Should().Be("Running");
    }

    [Fact]
    public void GetRunStatus_WhenRunDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var runId = "invalid-run";
        object? cachedVal = null;
        _memoryCache.TryGetValue(("golden-set-run", "test-tenant", runId), out cachedVal).Returns(false);

        // Act
        var result = _sut.GetRunStatus(runId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetRunStatus_WhenRunBelongsToDifferentTenant_ReturnsNotFound()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var runId = "tenant-a-run";
        cache.Set(("golden-set-run", "tenant-a", runId), new GoldenSetRunStatusDto
        {
            RunId = runId,
            Status = "Completed"
        });
        var controller = new GoldenSetController(_repository, _agentFactory, _evaluationService,
            _tenantContextAccessor, cache, _serviceProvider, _logger);

        _tenantContextAccessor.CurrentTenantId.Returns("tenant-a");
        controller.GetRunStatus(runId).Should().BeOfType<OkObjectResult>();

        _tenantContextAccessor.CurrentTenantId.Returns("tenant-b");
        controller.GetRunStatus(runId).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Run_WhenCasesCountGreaterThan20_ReturnsAccepted()
    {
        // Arrange
        var goldenSet = new GoldenSet
        {
            Id = "gs_large",
            TenantId = "test-tenant",
            Name = "Large Run Set",
            AgentName = "agent_1",
            Cases = Enumerable.Range(1, 25).Select(i => new GoldenSetCase
            {
                Id = $"c{i}",
                Input = $"Input {i}",
                ExpectedOutput = $"Expected {i}"
            }).ToList()
        };

        _repository.GetByIdAsync("gs_large", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(goldenSet);

        // Act
        var result = await _sut.Run("gs_large", CancellationToken.None);

        // Assert
        var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
        acceptedResult.Value.Should().NotBeNull();

        var valueType = acceptedResult.Value.GetType();
        var runIdProp = valueType.GetProperty("runId");
        var statusProp = valueType.GetProperty("status");
        var messageProp = valueType.GetProperty("message");

        runIdProp.Should().NotBeNull();
        var runId = runIdProp!.GetValue(acceptedResult.Value) as string;
        runId.Should().NotBeNullOrEmpty();

        statusProp.Should().NotBeNull();
        var status = statusProp!.GetValue(acceptedResult.Value) as string;
        status.Should().Be("Running");

        messageProp.Should().NotBeNull();
        var message = messageProp!.GetValue(acceptedResult.Value) as string;
        message.Should().Be("Evaluation started asynchronously.");

        _memoryCache.Received(1).CreateEntry(("golden-set-run", "test-tenant", runId));
    }

    [Fact]
    public async Task Run_WhenCasesCountLessThanOrEqualTo20_ReturnsOk()
    {
        // Arrange
        var goldenSet = new GoldenSet
        {
            Id = "gs_small",
            TenantId = "test-tenant",
            Name = "Small Run Set",
            AgentName = "agent_1",
            Cases = new List<GoldenSetCase>
            {
                new GoldenSetCase { Id = "c1", Input = "Input 1", ExpectedOutput = "Expected 1" }
            }
        };

        _repository.GetByIdAsync("gs_small", "test-tenant", Arg.Any<CancellationToken>())
            .Returns(goldenSet);

        var suiteResult = new EvalSuiteResult
        {
            SuiteId = "suite_abc",
            AgentName = "agent_1",
            TotalTests = 1,
            Passed = 1,
            OverallScore = 0.95,
            CompletedAt = DateTime.UtcNow
        };

        _evaluationService.RunSuiteAsync(Arg.Any<IReadOnlyList<EvalTestCase>>(), null, Arg.Any<CancellationToken>())
            .Returns(suiteResult);

        // Act
        var result = await _sut.Run("gs_small", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var runResult = okResult.Value as GoldenSetRunResultDto;
        runResult.Should().NotBeNull();
        runResult!.SuiteId.Should().Be("suite_abc");
    }

    [Fact]
    public async Task GetById_WhenGoldenSetBelongsToDifferentTenant_ReturnsNotFound()
    {
        // Arrange
        _repository.GetByIdAsync("gs_tenantB", "tenantA", Arg.Any<CancellationToken>())
            .Returns((GoldenSet?)null);

        _tenantContextAccessor.CurrentTenantId.Returns("tenantA");

        // Act
        var result = await _sut.GetById("gs_tenantB", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Run_WhenGoldenSetBelongsToDifferentTenant_ReturnsNotFound()
    {
        // Arrange
        _repository.GetByIdAsync("gs_tenantB", "tenantA", Arg.Any<CancellationToken>())
            .Returns((GoldenSet?)null);

        _tenantContextAccessor.CurrentTenantId.Returns("tenantA");

        // Act
        var result = await _sut.Run("gs_tenantB", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_WhenGoldenSetBelongsToDifferentTenant_ReturnsNotFound()
    {
        // Arrange
        _repository.GetByIdAsync("gs_tenantB", "tenantA", Arg.Any<CancellationToken>())
            .Returns((GoldenSet?)null);

        _tenantContextAccessor.CurrentTenantId.Returns("tenantA");

        // Act
        var result = await _sut.Delete("gs_tenantB", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
