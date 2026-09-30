using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;

namespace AgenticSystem.Tests;

public sealed class FidesDataProtectionMiddlewareTests
{
    [Fact]
    public async Task TextDetectorsMaskSensitiveContentBeforeCallingProvider()
    {
        using var tenantScope = CreateTenantScope(out var tenantAccessor);
        var inner = new CapturingAgent();
        var sut = CreateMiddleware(inner, tenantAccessor, new InMemoryFidesTenantPolicyStore(tenantAccessor), new FailClosedFidesMediaScanner());
        var messages = new[] { new ChatMessage(ChatRole.User, "Meu CPF é 123.456.789-09 e email jon@example.com") };

        await sut.RunAsync(messages, session: null, options: null, cancellationToken: CancellationToken.None);

        inner.InvocationCount.Should().Be(1);
        var forwarded = inner.LastMessages!;
        forwarded.Single().Text.Should().Contain("[CPF MASCARADO]");
        forwarded.Single().Text.Should().Contain("[EMAIL MASCARADO]");
        forwarded.Single().Text.Should().NotContain("123.456.789-09");
    }

    [Fact]
    public async Task CredentialTokenDetectorCannotBeDisabledEvenIfStoredPolicyIsInvalid()
    {
        using var tenantScope = CreateTenantScope(out var tenantAccessor);
        var invalidPolicy = FidesDetectorCatalog.Normalize(null);
        invalidPolicy[FidesDetectorCatalog.CredentialToken] = false;
        var policyStore = new FixedFidesPolicyStore(new FidesTenantPolicy
        {
            TenantId = "tenant-a",
            EnabledDetectors = invalidPolicy
        });
        var inner = new CapturingAgent();
        var sut = CreateMiddleware(inner, tenantAccessor, policyStore, new FailClosedFidesMediaScanner());
        var token = "sk-" + new string('a', 40);

        await sut.RunAsync([new ChatMessage(ChatRole.User, $"token={token}")], null, null, CancellationToken.None);

        var forwarded = inner.LastMessages!;
        forwarded.Single().Text.Should().Contain("[TOKEN MASCARADO]");
        forwarded.Single().Text.Should().NotContain(token);
    }

    [Fact]
    public async Task UncertainMediaScanBlocksBeforeCallingProvider()
    {
        using var tenantScope = CreateTenantScope(out var tenantAccessor);
        var mediaScanner = new UncertainMediaScanner();
        var inner = new CapturingAgent();
        var sut = CreateMiddleware(inner, tenantAccessor, new InMemoryFidesTenantPolicyStore(tenantAccessor), mediaScanner);
        var message = new ChatMessage(ChatRole.User, string.Empty);
        message.Contents.Add(new DataContent(new byte[] { 1, 2, 3 }, "image/png"));

        var response = await sut.RunAsync([message], session: null, options: null, cancellationToken: CancellationToken.None);

        response.Text.Should().Contain("versão textual");
        inner.InvocationCount.Should().Be(0);
        mediaScanner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task MediaScanTimeoutBlocksWhenScannerDoesNotHonorCancellation()
    {
        using var tenantScope = CreateTenantScope(out var tenantAccessor);
        var inner = new CapturingAgent();
        var sut = CreateMiddleware(
            inner,
            tenantAccessor,
            new InMemoryFidesTenantPolicyStore(tenantAccessor),
            new HangingMediaScanner(),
            mediaScanTimeoutSeconds: 1);
        var message = new ChatMessage(ChatRole.User, string.Empty);
        message.Contents.Add(new DataContent(new byte[] { 1, 2, 3 }, "application/pdf"));

        var response = await sut.RunAsync([message], session: null, options: null, cancellationToken: CancellationToken.None);

        response.Text.Should().Contain("versão textual");
        inner.InvocationCount.Should().Be(0);
    }

    private static IDisposable CreateTenantScope(out ITenantContextAccessor accessor)
    {
        var concreteAccessor = new TenantContextAccessor();
        accessor = concreteAccessor;
        return concreteAccessor.BeginScope(new TenantContext { TenantId = "tenant-a" });
    }

    private static FidesDataProtectionMiddleware CreateMiddleware(
        AIAgent inner,
        ITenantContextAccessor tenantAccessor,
        IFidesTenantPolicyStore policyStore,
        IFidesMediaScanner mediaScanner,
        int mediaScanTimeoutSeconds = 10) => new(
        inner,
        tenantAccessor,
        policyStore,
        mediaScanner,
        Options.Create(new FidesSecuritySettings { MediaScanTimeoutSeconds = mediaScanTimeoutSeconds }),
        NullLogger<FidesDataProtectionMiddleware>.Instance);

    private sealed class CapturingAgent : AIAgent
    {
        public int InvocationCount { get; private set; }
        public List<ChatMessage>? LastMessages { get; private set; }

        protected override Task<Microsoft.Agents.AI.AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            LastMessages = messages.ToList();
            return Task.FromResult(new Microsoft.Agents.AI.AgentResponse(
                new ChatMessage(ChatRole.Assistant, "ok")));
        }

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(Substitute.For<AgentSession>());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(JsonDocument.Parse("{}").RootElement.Clone());

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Substitute.For<AgentSession>());

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class UncertainMediaScanner : IFidesMediaScanner
    {
        public int Calls { get; private set; }

        public Task<FidesMediaScanResult> ScanAndRedactAsync(
            ReadOnlyMemory<byte> content,
            string mediaType,
            FidesTenantPolicy policy,
            CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new FidesMediaScanResult { Status = FidesMediaScanStatus.Uncertain });
        }
    }

    private sealed class HangingMediaScanner : IFidesMediaScanner
    {
        public Task<FidesMediaScanResult> ScanAndRedactAsync(
            ReadOnlyMemory<byte> content,
            string mediaType,
            FidesTenantPolicy policy,
            CancellationToken ct = default) =>
            new TaskCompletionSource<FidesMediaScanResult>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
    }

    private sealed class FixedFidesPolicyStore : IFidesTenantPolicyStore
    {
        private readonly FidesTenantPolicy _policy;

        public FixedFidesPolicyStore(FidesTenantPolicy policy) => _policy = policy;

        public Task<FidesTenantPolicy> GetAsync(CancellationToken ct = default) => Task.FromResult(_policy);

        public Task<FidesTenantPolicy> SaveAsync(
            IReadOnlyDictionary<string, bool> enabledDetectors,
            string updatedBy,
            CancellationToken ct = default) => Task.FromResult(_policy);
    }
}
