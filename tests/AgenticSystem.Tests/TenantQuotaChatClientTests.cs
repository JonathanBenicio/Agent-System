using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Core.Exceptions;
using AgenticSystem.Infrastructure.LLM;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgenticSystem.Tests;

public class TenantQuotaChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_ChecksBeforeProviderAndRecordsActualUsage()
    {
        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "answer"))
            {
                Usage = new UsageDetails
                {
                    InputTokenCount = 7,
                    OutputTokenCount = 4,
                    CachedInputTokenCount = 1,
                    TotalTokenCount = 11
                }
            });
        var quota = Substitute.For<IQuotaEnforcer>();
        quota.CheckQuotaAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QuotaCheckResult { Allowed = true }));
        var audit = Substitute.For<ITokenAuditService>();
        audit.CalculateCostAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0.005m));
        var contextAccessor = new LLMRuntimeContextAccessor();
        var client = new TenantQuotaChatClient(
            inner, "Ollama", "qwen2.5:0.5b", contextAccessor, quota, audit,
            NullLogger<TenantQuotaChatClient>.Instance);

        using (contextAccessor.BeginScope(new UserContext
        {
            UserId = "user-1", TenantId = "tenant-1", Preferences = new Dictionary<string, object>()
        }, "session-1"))
        {
            var response = await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "prompt")], new ChatOptions { MaxOutputTokens = 20 });

            response.Text.Should().Be("answer");
            await quota.Received(1).CheckQuotaAsync("tenant-1", 22, 0.005d, Arg.Any<CancellationToken>());
            await quota.Received(1).RecordUsageAsync("tenant-1", 11, 0.005d, Arg.Any<CancellationToken>());
            await audit.Received(1).RecordTokenUsageAsync(Arg.Is<TokenUsageRecord>(record =>
                record.TenantId == "tenant-1" && record.SessionId == "session-1" &&
                record.Provider == "Ollama" && record.ModelId == "qwen2.5:0.5b" &&
                record.PromptTokens == 7 && record.CompletionTokens == 4 && record.CachedTokens == 1 &&
                record.CalculatedCost == 0.005m), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task GetResponseAsync_WhenDailyQuotaIsExceeded_DoesNotCallProviderOrRecordUsage()
    {
        var inner = Substitute.For<IChatClient>();
        var quota = Substitute.For<IQuotaEnforcer>();
        quota.CheckQuotaAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QuotaCheckResult { Allowed = false, DenialReason = "Daily token quota exceeded" }));
        var audit = Substitute.For<ITokenAuditService>();
        audit.CalculateCostAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0.001m));
        var contextAccessor = new LLMRuntimeContextAccessor();
        var client = new TenantQuotaChatClient(
            inner, "Ollama", "qwen2.5:0.5b", contextAccessor, quota, audit,
            NullLogger<TenantQuotaChatClient>.Instance);

        using (contextAccessor.BeginScope(new UserContext
        {
            UserId = "user-1", TenantId = "tenant-1", Preferences = new Dictionary<string, object>()
        }))
        {
            var act = () => client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "prompt")], new ChatOptions { MaxOutputTokens = 20 });

            await act.Should().ThrowAsync<QuotaExceededException>()
                .WithMessage("Quota Exceeded: Daily token quota exceeded");
            await inner.DidNotReceive().GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
            await quota.DidNotReceive().RecordUsageAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task GetResponseAsync_WhenProviderOmitsUsage_RecordsConservativeTokenEstimates()
    {
        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "answer"))
            {
                Usage = new UsageDetails()
            });
        var quota = Substitute.For<IQuotaEnforcer>();
        quota.CheckQuotaAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QuotaCheckResult { Allowed = true }));
        var audit = Substitute.For<ITokenAuditService>();
        audit.CalculateCostAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0.002m));
        var contextAccessor = new LLMRuntimeContextAccessor();
        var client = new TenantQuotaChatClient(
            inner, "Ollama", "qwen2.5:0.5b", contextAccessor, quota, audit,
            NullLogger<TenantQuotaChatClient>.Instance);

        using (contextAccessor.BeginScope(new UserContext
        {
            UserId = "user-1", TenantId = "tenant-1", Preferences = new Dictionary<string, object>()
        }))
        {
            _ = await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "prompt")], new ChatOptions { MaxOutputTokens = 20 });

            await quota.Received(1).RecordUsageAsync("tenant-1", 4, 0.002d, Arg.Any<CancellationToken>());
            await audit.Received(1).RecordTokenUsageAsync(Arg.Is<TokenUsageRecord>(record =>
                record.PromptTokens == 2 && record.CompletionTokens == 2), Arg.Any<CancellationToken>());
        }
    }
}
