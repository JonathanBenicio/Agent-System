using System.Runtime.CompilerServices;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Gateway;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgenticSystem.Tests;

public class ServiceGatewayStreamingTests
{
    [Fact]
    public async Task ExecuteStreamingAsync_RecordsSuccessAfterFullEnumeration()
    {
        var gateway = CreateGateway();
        gateway.RegisterService(new ServiceRegistration { Name = "Ollama", Category = "LLM" });

        var result = new List<int>();
        await foreach (var item in gateway.ExecuteStreamingAsync("Ollama", _ => Values()))
            result.Add(item);

        result.Should().Equal(1, 2);
        var status = await gateway.GetServiceStatusAsync("Ollama");
        status.RequestCount.Should().Be(1);
        status.FailureCount.Should().Be(0);
        status.IsHealthy.Should().BeTrue();
        (await gateway.GetCostReportAsync()).TotalCost.Should().Be(0.001m);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_RecordsFailureAfterPartialOutput()
    {
        var gateway = CreateGateway();
        gateway.RegisterService(new ServiceRegistration { Name = "OpenAI", Category = "LLM" });
        await using var stream = gateway.ExecuteStreamingAsync("OpenAI", _ => FailsAfterFirstItem()).GetAsyncEnumerator();

        (await stream.MoveNextAsync()).Should().BeTrue();
        stream.Current.Should().Be(1);
        var act = () => stream.MoveNextAsync().AsTask();

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("provider stream failed");
        var status = await gateway.GetServiceStatusAsync("OpenAI");
        status.RequestCount.Should().Be(1);
        status.FailureCount.Should().Be(1);
        status.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteStreamingAsync_CancellationDoesNotCountAsProviderFailure()
    {
        var gateway = CreateGateway();
        gateway.RegisterService(new ServiceRegistration { Name = "Gemini", Category = "LLM" });
        using var cancellation = new CancellationTokenSource();
        await using var stream = gateway.ExecuteStreamingAsync("Gemini", ct => WaitAfterFirstItem(ct), cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);

        (await stream.MoveNextAsync()).Should().BeTrue();
        cancellation.Cancel();
        var act = () => stream.MoveNextAsync().AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
        var status = await gateway.GetServiceStatusAsync("Gemini");
        status.RequestCount.Should().Be(1);
        status.FailureCount.Should().Be(0);
        status.IsHealthy.Should().BeTrue();
    }

    private static ServiceGateway CreateGateway() => new(
        new CostTracker(10m),
        Substitute.For<ILogger<ServiceGateway>>());

    private static async IAsyncEnumerable<int> Values()
    {
        await Task.Yield();
        yield return 1;
        yield return 2;
    }

    private static async IAsyncEnumerable<int> FailsAfterFirstItem()
    {
        yield return 1;
        await Task.Yield();
        throw new HttpRequestException("provider stream failed");
    }

    private static async IAsyncEnumerable<int> WaitAfterFirstItem([EnumeratorCancellation] CancellationToken ct)
    {
        yield return 1;
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }
}
