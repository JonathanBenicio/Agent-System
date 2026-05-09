using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Extensions;
using AgenticSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class PostgresQualityStoresDIIntegrationTests
{
    [Fact]
    public void UsePostgresQualityStores_ReplacesInMemoryRegistrations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAgentVersionStore, AgenticSystem.Core.Services.InMemoryAgentVersionStore>();
        services.AddSingleton<IPromptTemplateStore, AgenticSystem.Core.Services.InMemoryPromptTemplateStore>();
        services.AddSingleton<IEvalResultStore, AgenticSystem.Core.Services.InMemoryEvalResultStore>();

        services.UsePostgresQualityStores("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAgentVersionStore>().Should().BeOfType<PostgresAgentVersionStore>();
        provider.GetRequiredService<IPromptTemplateStore>().Should().BeOfType<PostgresPromptTemplateStore>();
        provider.GetRequiredService<IEvalResultStore>().Should().BeOfType<PostgresEvalResultStore>();
    }
}

public class PostgresAgentVersionStoreTests
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly PostgresAgentVersionStore _sut;

    public PostgresAgentVersionStoreTests()
    {
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"agent-version-tests-{Guid.NewGuid():N}")
            .Options;

        var context = new AgenticDbContext(options);
        context.Database.EnsureCreated();

        var factory = Substitute.For<IDbContextFactory<AgenticDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AgenticDbContext(options)));

        _dbContextFactory = factory;
        _sut = new PostgresAgentVersionStore(_dbContextFactory, NullLogger<PostgresAgentVersionStore>.Instance);
    }

    [Fact]
    public async Task SaveAndRetrieve_WorksCorrectly()
    {
        var version = new AgentVersion
        {
            Id = "v1",
            AgentName = "TestAgent",
            VersionNumber = 1,
            Label = "v1.0",
            Status = AgentVersionStatus.Active,
            Environment = AgentVersionEnvironment.Staging,
            SystemPrompt = "Hello Prompt",
            ModelProvider = "openai",
            ModelId = "gpt-4",
            Tools = new List<string> { "tool1", "tool2" },
            Parameters = new Dictionary<string, object> { { "temp", 0.7 } },
            CreatedAt = DateTime.UtcNow
        };

        await _sut.SaveAsync(version);

        var retrieved = await _sut.GetByIdAsync("v1");
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be("v1");
        retrieved.AgentName.Should().Be("TestAgent");
        retrieved.VersionNumber.Should().Be(1);
        retrieved.Tools.Should().ContainInOrder("tool1", "tool2");
        retrieved.Parameters.Should().ContainKey("temp");
        var tempValue = retrieved.Parameters["temp"];
        if (tempValue is System.Text.Json.JsonElement elem)
        {
            elem.GetDouble().Should().Be(0.7);
        }
        else
        {
            tempValue.Should().Be(0.7);
        }
    }

    [Fact]
    public async Task GetActive_ReturnsCorrectVersion()
    {
        var version1 = new AgentVersion
        {
            Id = "v1",
            AgentName = "TestAgent",
            VersionNumber = 1,
            Status = AgentVersionStatus.Deprecated,
            Environment = AgentVersionEnvironment.Production,
            SystemPrompt = "Prompt 1",
            Tools = new List<string>(),
            Parameters = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var version2 = new AgentVersion
        {
            Id = "v2",
            AgentName = "TestAgent",
            VersionNumber = 2,
            Status = AgentVersionStatus.Active,
            Environment = AgentVersionEnvironment.Production,
            SystemPrompt = "Prompt 2",
            Tools = new List<string>(),
            Parameters = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow
        };

        await _sut.SaveAsync(version1);
        await _sut.SaveAsync(version2);

        var active = await _sut.GetActiveAsync("TestAgent", AgentVersionEnvironment.Production);
        active.Should().NotBeNull();
        active!.Id.Should().Be("v2");
        active.SystemPrompt.Should().Be("Prompt 2");
    }

    [Fact]
    public async Task GetHistory_ReturnsOrderedList()
    {
        for (int i = 1; i <= 5; i++)
        {
            await _sut.SaveAsync(new AgentVersion
            {
                Id = $"v{i}",
                AgentName = "TestAgent",
                VersionNumber = i,
                SystemPrompt = $"Prompt {i}",
                Tools = new List<string>(),
                Parameters = new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }

        var history = await _sut.GetHistoryAsync("TestAgent", limit: 3);
        history.Should().HaveCount(3);
        history[0].Id.Should().Be("v5");
        history[1].Id.Should().Be("v4");
        history[2].Id.Should().Be("v3");
    }

    [Fact]
    public async Task GetNextVersionNumber_CalculatesCorrectly()
    {
        var next1 = await _sut.GetNextVersionNumberAsync("NewAgent");
        next1.Should().Be(1);

        await _sut.SaveAsync(new AgentVersion
        {
            Id = "v1",
            AgentName = "NewAgent",
            VersionNumber = 5,
            SystemPrompt = "Prompt",
            Tools = new List<string>(),
            Parameters = new Dictionary<string, object>()
        });

        var next2 = await _sut.GetNextVersionNumberAsync("NewAgent");
        next2.Should().Be(6);
    }
}

public class PostgresPromptTemplateStoreTests
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly PostgresPromptTemplateStore _sut;

    public PostgresPromptTemplateStoreTests()
    {
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"prompt-template-tests-{Guid.NewGuid():N}")
            .Options;

        var context = new AgenticDbContext(options);
        context.Database.EnsureCreated();

        var factory = Substitute.For<IDbContextFactory<AgenticDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AgenticDbContext(options)));

        _dbContextFactory = factory;
        _sut = new PostgresPromptTemplateStore(_dbContextFactory, NullLogger<PostgresPromptTemplateStore>.Instance);
    }

    [Fact]
    public async Task SaveAndRetrieve_WorksCorrectly()
    {
        var template = new PromptTemplate
        {
            Id = "p1",
            Name = "Welcome",
            AgentName = "AgentX",
            TemplateBody = "Hello {user}",
            Version = 1,
            Locale = "pt-BR",
            Variables = new List<string> { "user" },
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _sut.SaveAsync(template);

        var active = await _sut.GetActiveAsync("AgentX", "pt-BR");
        active.Should().NotBeNull();
        active!.Id.Should().Be("p1");
        active.TemplateBody.Should().Be("Hello {user}");
        active.Variables.Should().Contain("user");
    }

    [Fact]
    public async Task GetAllForAgent_ReturnsOrderedList()
    {
        await _sut.SaveAsync(new PromptTemplate
        {
            Id = "p1",
            Name = "Welcome V1",
            AgentName = "AgentX",
            TemplateBody = "Hello",
            Version = 1,
            Locale = "pt-BR",
            Variables = new List<string>(),
            IsActive = false
        });

        await _sut.SaveAsync(new PromptTemplate
        {
            Id = "p2",
            Name = "Welcome V2",
            AgentName = "AgentX",
            TemplateBody = "Hello V2",
            Version = 2,
            Locale = "pt-BR",
            Variables = new List<string>(),
            IsActive = true
        });

        var templates = await _sut.GetAllForAgentAsync("AgentX");
        templates.Should().HaveCount(2);
        templates[0].Version.Should().Be(2);
        templates[1].Version.Should().Be(1);
    }
}

public class PostgresEvalResultStoreTests
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly PostgresEvalResultStore _sut;

    public PostgresEvalResultStoreTests()
    {
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"eval-result-tests-{Guid.NewGuid():N}")
            .Options;

        var context = new AgenticDbContext(options);
        context.Database.EnsureCreated();

        var factory = Substitute.For<IDbContextFactory<AgenticDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AgenticDbContext(options)));

        _dbContextFactory = factory;
        _sut = new PostgresEvalResultStore(_dbContextFactory, NullLogger<PostgresEvalResultStore>.Instance);
    }

    [Fact]
    public async Task SaveAndRetrieve_WorksCorrectly()
    {
        var result = new EvalSuiteResult
        {
            SuiteId = "suite1",
            AgentName = "AgentY",
            AgentVersionId = "v1.0",
            TotalTests = 10,
            Passed = 9,
            Failed = 1,
            OverallScore = 0.9,
            AccuracyScore = 0.95,
            SafetyScore = 1.0,
            LatencyP50Ms = 120.0,
            LatencyP95Ms = 450.0,
            TotalTokensUsed = 1200,
            Results = new List<EvalTestResult>
            {
                new()
                {
                    TestCaseId = "t1",
                    TestCaseName = "TestCase1",
                    AgentName = "AgentY",
                    ActualOutput = "Hello",
                    Passed = true,
                    Score = 1.0,
                    Latency = TimeSpan.FromMilliseconds(100)
                }
            },
            Regressions = new List<EvalRegressionAlert>(),
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow
        };

        await _sut.SaveSuiteResultAsync(result);

        var baseline = await _sut.GetLatestBaselineAsync("AgentY");
        baseline.Should().NotBeNull();
        baseline!.SuiteId.Should().Be("suite1");
        baseline.Passed.Should().Be(9);
        baseline.OverallScore.Should().Be(0.9);
        baseline.Results.Should().HaveCount(1);
        baseline.Results[0].TestCaseId.Should().Be("t1");
    }

    [Fact]
    public async Task GetHistory_ReturnsOrderedList()
    {
        for (int i = 1; i <= 3; i++)
        {
            await _sut.SaveSuiteResultAsync(new EvalSuiteResult
            {
                SuiteId = $"suite{i}",
                AgentName = "AgentY",
                Results = new List<EvalTestResult>(),
                Regressions = new List<EvalRegressionAlert>(),
                StartedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }

        var history = await _sut.GetHistoryAsync("AgentY", limit: 2);
        history.Should().HaveCount(2);
        history[0].SuiteId.Should().Be("suite3");
        history[1].SuiteId.Should().Be("suite2");
    }
}
