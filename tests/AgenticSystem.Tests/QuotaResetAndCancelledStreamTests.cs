using System.Runtime.CompilerServices;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.LLM;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgenticSystem.Tests;

public sealed class QuotaResetAndCancelledStreamTests
{
    [Fact]
    public async Task DailyReset_IsUtcScopedAndPreservesConfigurationAndCurrentDayUsage()
    {
        var clock = new Clock(new DateTimeOffset(2026, 10, 2, 23, 59, 0, TimeSpan.Zero));
        var repository = new InMemoryTenantQuotaRepository(clock);
        await repository.UpsertConfigAsync("a", new QuotaConfig
        {
            RequestsPerMinute = 17, MaxTokensPerDay = 200, MaxDailyBudgetUsd = 2
        });
        await repository.IncrementUsageAsync("a", 100, 1);
        await repository.IncrementUsageAsync("b", 80, 0.8);
        await repository.ResetDailyCountersAsync("a");
        (await repository.GetOrCreateAsync("a")).CurrentDailyTokens.Should().Be(100);
        clock.Advance(TimeSpan.FromMinutes(2));
        var fresh = await repository.GetOrCreateAsync("a");
        fresh.CurrentDailyTokens.Should().Be(0);
        fresh.CurrentDailyCostUsd.Should().Be(0);
        fresh.CurrentDailyRequests.Should().Be(0);
        fresh.MaxTokensPerDay.Should().Be(200);
        fresh.RequestsPerMinute.Should().Be(17);
        fresh.MaxDailyBudgetUsd.Should().Be(2);
        await repository.IncrementUsageAsync("b", 2, 0.02);
        (await repository.GetOrCreateAsync("b")).CurrentDailyTokens.Should().Be(2);
        await repository.IncrementUsageAsync("a", 7, 0.07);
        await repository.ResetDailyCountersAsync("a");
        (await repository.GetOrCreateAsync("a")).CurrentDailyTokens.Should().Be(7);
    }

    [Fact]
    public async Task DailyReset_ConcurrentFirstWritesDoNotLoseUsage()
    {
        var clock = new Clock(new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero));
        var repository = new InMemoryTenantQuotaRepository(clock);
        await repository.IncrementUsageAsync("a", 90, 1);
        clock.Advance(TimeSpan.FromDays(1));
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            repository.IncrementUsageAsync("a", 1, 0.01))));
        var snapshot = await repository.GetOrCreateAsync("a");
        snapshot.CurrentDailyTokens.Should().Be(32);
        snapshot.CurrentDailyRequests.Should().Be(32);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("break")]
    [InlineData("error")]
    [InlineData("complete")]
    public async Task Streaming_PersistsDeliveredUsageOnceWithIndependentCancellation(string exit)
    {
        var inner = Substitute.For<IChatClient>();
        var cancel = new CancellationTokenSource();
        inner.GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>()).Returns(call => Stream(exit, call.ArgAt<CancellationToken>(2)));
        var quota = Substitute.For<IQuotaEnforcer>();
        quota.CheckQuotaAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult { Allowed = true });
        quota.RecordUsageAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.ArgAt<CancellationToken>(3).ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
        var audit = Substitute.For<ITokenAuditService>();
        audit.CalculateCostAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0.005m);
        var runtime = new LLMRuntimeContextAccessor();
        using var scope = runtime.BeginScope(new UserContext { UserId = "u", TenantId = "a" }, "s");
        var client = new TenantQuotaChatClient(inner, "review", "model", runtime, quota, audit,
            NullLogger<TenantQuotaChatClient>.Instance);
        var consume = async () =>
        {
            await foreach (var update in client.GetStreamingResponseAsync(
                               [new ChatMessage(ChatRole.User, "prompt")], cancellationToken: cancel.Token))
            {
                update.Text.Should().Be("part");
                if (exit == "break") break;
                if (exit == "cancel") cancel.Cancel();
            }
        };
        if (exit == "cancel") await consume.Should().ThrowAsync<OperationCanceledException>();
        else if (exit == "error") await consume.Should().ThrowAsync<InvalidOperationException>();
        else await consume();
        await quota.Received(1).RecordUsageAsync("a", 11, 0.005,
            Arg.Is<CancellationToken>(token => !token.IsCancellationRequested));
        await audit.Received(1).RecordTokenUsageAsync(
            Arg.Is<TokenUsageRecord>(record => record.PromptTokens == 7 && record.CompletionTokens == 4),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Streaming_AbortedWithoutUsage_DoesNotFabricateCompletedConsumption()
    {
        var inner = Substitute.For<IChatClient>();
        inner.GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>()).Returns(_ => EmptyFailure());
        var quota = Substitute.For<IQuotaEnforcer>();
        quota.CheckQuotaAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(new QuotaCheckResult { Allowed = true });
        var runtime = new LLMRuntimeContextAccessor();
        using var scope = runtime.BeginScope(new UserContext { UserId = "u", TenantId = "a" });
        var client = new TenantQuotaChatClient(inner, "review", "model", runtime, quota,
            Substitute.For<ITokenAuditService>(), NullLogger<TenantQuotaChatClient>.Instance);
        var consume = async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "prompt")])) { }
        };
        await consume.Should().ThrowAsync<InvalidOperationException>();
        await quota.DidNotReceiveWithAnyArgs().RecordUsageAsync(default!, default, default, default);
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Stream(
        string exit, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "part")
        {
            Contents = [new TextContent("part"), new UsageContent(new UsageDetails
                { InputTokenCount = 7, OutputTokenCount = 4, TotalTokenCount = 11 })]
        };
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        if (exit == "error") throw new InvalidOperationException("Provider interrupted.");
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> EmptyFailure()
    {
        await Task.Yield();
        throw new InvalidOperationException("No response received.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
