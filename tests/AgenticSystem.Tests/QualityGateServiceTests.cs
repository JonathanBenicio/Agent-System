using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class QualityGateServiceTests
{
    private readonly ILogger<QualityGateService> _logger;

    public QualityGateServiceTests()
    {
        _logger = Substitute.For<ILogger<QualityGateService>>();
    }

    [Fact]
    public async Task ValidateRequest_ValidInput_Passes()
    {
        var gates = new IQualityGate[] { new InputValidationGate() };
        var sut = new QualityGateService(gates, _logger);

        var report = await sut.ValidateRequestAsync("Hello, can you help me?");

        report.OverallPassed.Should().BeTrue();
        report.Phase.Should().Be(QualityGatePhase.PreExecution);
        report.AverageScore.Should().Be(10);
    }

    [Fact]
    public async Task ValidateRequest_EmptyInput_Fails()
    {
        var gates = new IQualityGate[] { new InputValidationGate() };
        var sut = new QualityGateService(gates, _logger);

        var report = await sut.ValidateRequestAsync("");

        report.OverallPassed.Should().BeFalse();
        report.Results.Should().ContainSingle()
            .Which.Issues.Should().Contain("Input is empty or whitespace");
    }

    [Fact]
    public async Task ValidateRequest_TooShortInput_Fails()
    {
        var gates = new IQualityGate[] { new InputValidationGate() };
        var sut = new QualityGateService(gates, _logger);

        var report = await sut.ValidateRequestAsync("Hi");

        report.OverallPassed.Should().BeFalse();
        report.Results.Should().ContainSingle()
            .Which.Issues.Should().Contain("Input is too short (< 3 chars)");
    }

    [Fact]
    public async Task ValidateResponse_ValidOutput_Passes()
    {
        var gates = new IQualityGate[] { new ResponseQualityGate() };
        var sut = new QualityGateService(gates, _logger);

        var report = await sut.ValidateResponseAsync("test input", "Here is a detailed response to your question");

        report.OverallPassed.Should().BeTrue();
        report.Phase.Should().Be(QualityGatePhase.PostExecution);
    }

    [Fact]
    public async Task ValidateResponse_EmptyOutput_Fails()
    {
        var gates = new IQualityGate[] { new ResponseQualityGate() };
        var sut = new QualityGateService(gates, _logger);

        var report = await sut.ValidateResponseAsync("test input", "");

        report.OverallPassed.Should().BeFalse();
        report.Results.Should().ContainSingle()
            .Which.Issues.Should().Contain("Output is empty");
    }

    [Fact]
    public async Task ValidateRequest_SkipsPostExecutionGates()
    {
        var gates = new IQualityGate[] { new InputValidationGate(), new ResponseQualityGate() };
        var sut = new QualityGateService(gates, _logger);

        var report = await sut.ValidateRequestAsync("Valid input");

        report.Results.Should().HaveCount(1);
        report.Results[0].GateName.Should().Be("InputValidation");
    }

    [Fact]
    public async Task RegisterGate_AddsGateDynamically()
    {
        var sut = new QualityGateService(Enumerable.Empty<IQualityGate>(), _logger);

        sut.GetRegisteredGates().Should().BeEmpty();

        sut.RegisterGate(new InputValidationGate());

        sut.GetRegisteredGates().Should().HaveCount(1);
    }

    [Fact]
    public async Task GateThatThrows_IsHandledGracefully()
    {
        var faultyGate = Substitute.For<IQualityGate>();
        faultyGate.Name.Returns("FaultyGate");
        faultyGate.Phase.Returns(QualityGatePhase.PreExecution);
        faultyGate.Order.Returns(1);
        faultyGate.ValidateAsync(Arg.Any<QualityContext>(), Arg.Any<CancellationToken>())
            .Returns<QualityResult>(x => throw new InvalidOperationException("Boom"));

        var sut = new QualityGateService(new[] { faultyGate }, _logger);

        var report = await sut.ValidateRequestAsync("some input");

        report.OverallPassed.Should().BeFalse();
        report.Results.Should().ContainSingle()
            .Which.Issues.Should().Contain(i => i.Contains("Gate error"));
    }
}
