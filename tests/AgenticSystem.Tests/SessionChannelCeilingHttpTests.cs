using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Api.Hubs;
using AgenticSystem.Api.Services;
using AgenticSystem.Core.Exceptions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Tests;

public sealed class SessionChannelCeilingHttpTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task HttpChat_RealSessionBoundaryRejectsNewAndResumesAtCeiling(bool stream, bool direct)
    {
        await using var fixture = await Fixture.Create();
        var first = await fixture.Send(stream, direct, null);
        first.IsSuccessStatusCode.Should().BeTrue();
        await first.Content.ReadAsStringAsync();
        var id = (await fixture.Store.GetByTenantAsync("tenant", "user")).Single().Id;
        (await fixture.Store.CountActiveAsync("tenant")).Should().Be(1);
        var before = fixture.DispatchCount;
        Func<Task> request = async () => { using var response = await fixture.Send(stream, direct, null); await response.Content.ReadAsStringAsync(); };
        await request.Should().ThrowAsync<QuotaExceededException>();
        fixture.DispatchCount.Should().Be(before);
        (await fixture.Store.CountActiveAsync("tenant")).Should().Be(1);
        using var resumed = await fixture.Send(stream, direct, id);
        resumed.IsSuccessStatusCode.Should().BeTrue();
        await resumed.Content.ReadAsStringAsync();
        fixture.DispatchCount.Should().Be(before + 1);
        (await fixture.Store.CountActiveAsync("tenant")).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignalRWire_RealHubRejectsNewAndResumesAtCeiling(bool direct)
    {
        await using var fixture = await Fixture.Create();
        using var socket = await fixture.App.GetTestServer().CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/hubs/chat"), default);
        await Send(socket, "{\"protocol\":\"json\",\"version\":1}\u001e");
        await Receive(socket);
        var first = await Invoke(socket, "1", direct, null);
        first.Should().Contain("ReceiveMessage");
        var id = (await fixture.Store.GetByTenantAsync("tenant", "user")).Single().Id;
        var before = fixture.DispatchCount;
        var rejected = await Invoke(socket, "2", direct, null);
        rejected.Should().Contain("ReceiveError");
        rejected.Should().NotContain("ReceiveMessage");
        fixture.DispatchCount.Should().Be(before);
        (await fixture.Store.CountActiveAsync("tenant")).Should().Be(1);
        var resumed = await Invoke(socket, "3", direct, id);
        resumed.Should().Contain("ReceiveMessage");
        fixture.DispatchCount.Should().Be(before + 1);
        (await fixture.Store.CountActiveAsync("tenant")).Should().Be(1);
    }

    private static async Task<string> Invoke(WebSocket socket, string invocationId, bool direct, string? sessionId)
    {
        await Send(socket, JsonSerializer.Serialize(new { type = 1, invocationId, target = "SendMessage",
            arguments = new object?[] { "hello", direct ? "chosen" : null, "Review", "review-model", null, sessionId, null } }) + "\u001e");
        var text = "";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!text.Contains($"\"invocationId\":\"{invocationId}\"", StringComparison.Ordinal))
            text += await Receive(socket, timeout.Token);
        return text;
    }

    private static Task Send(WebSocket socket, string text) => socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);
    private static async Task<string> Receive(WebSocket socket, CancellationToken ct = default)
    {
        var buffer = new byte[32768];
        var result = await socket.ReceiveAsync(buffer, ct);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public WebApplication App { get; }
        public InMemorySessionStore Store { get; } = new();
        public HttpClient Client { get; private set; } = null!;
        public int DispatchCount;
        private Fixture()
        {
            var tenant = Substitute.For<ITenantStore>();
            tenant.GetByIdAsync("tenant", Arg.Any<CancellationToken>()).Returns(new Tenant {
                Id = "tenant", IsActive = true, Plan = TenantPlan.Free, Limits = new TenantLimits { MaxConcurrentSessions = 1 } });
            var manager = new SessionManager(Store, Substitute.For<ISessionConsolidator>(), NullLogger<SessionManager>.Instance, tenantStore: tenant);
            var runtime = new AgentRuntimeCoordinator(manager, NullLogger<AgentRuntimeCoordinator>.Instance);
            var lifecycle = new SessionLifecycleCoordinator(manager, runtime, NullLogger<SessionLifecycleCoordinator>.Instance);
            var framework = Substitute.For<IFrameworkOrchestratorService>();
            framework.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>()).Returns(call => {
                Interlocked.Increment(ref DispatchCount); return AgentResponse.Ok("ok", "worker", AgentTier.Support); });
            var executor = Substitute.For<IDirectAgentRequestExecutor>();
            executor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
                Interlocked.Increment(ref DispatchCount); return AgentResponse.Ok("direct", "worker", AgentTier.Support); });
            var llm = Substitute.For<ILLMRuntimeContextAccessor>();
            llm.BeginScope(Arg.Any<UserContext>(), Arg.Any<string>()).Returns(Substitute.For<IDisposable>());
            var router = Substitute.For<ISmartRouter>();
            router.TriageAsync(Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>()).Returns((false, null, null));
            var analyzer = Substitute.For<IContextAnalyzer>();
            analyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<UserContext>()).Returns(new AnalysisResult());
            var meta = new MetaAgentOrchestrator(framework, executor, llm, Substitute.For<IAgentFactory>(), lifecycle, manager, analyzer, router);
            var admin = Substitute.For<ILLMAdministrationService>();
            admin.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
            {
                DefaultProvider = "Review",
                DefaultModel = "review-model",
                Providers = [new LLMProviderInfo { Name = "Review", IsEnabled = true, DefaultModel = "review-model", Models = ["review-model"] }]
            });
            var apiKeys = Substitute.For<ILLMProviderApiKeyService>();
            apiKeys.GetKeysByProviderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<AgenticSystem.Core.LLM.Models.LLMProviderApiKey>());
            var configuration = new ChatConfigurationService(admin, apiKeys, Substitute.For<IChatSettingsStore>(), Store);
            var accessor = Substitute.For<ITenantContextAccessor>(); accessor.CurrentTenantId.Returns("tenant");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(ChatController).Assembly.GetName().Name });
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<IMetaAgent>(meta);
            builder.Services.AddSingleton<ISessionStore>(Store);
            builder.Services.AddSingleton(accessor);
            builder.Services.AddSingleton(configuration);
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(ChatController).Assembly);
            builder.Services.AddSignalR();
            App = builder.Build();
            App.UseAuthentication(); App.UseAuthorization(); App.MapControllers(); App.MapHub<ChatHub>("/hubs/chat");
        }
        public static async Task<Fixture> Create() { var fixture = new Fixture(); await fixture.App.StartAsync(); fixture.Client = fixture.App.GetTestClient(); return fixture; }
        public Task<HttpResponseMessage> Send(bool stream, bool direct, string? id) => Client.PostAsJsonAsync(stream ? "/api/chat/stream" : "/api/chat",
            new { message = "hello", sessionId = id, targetAgent = direct ? "chosen" : null, provider = "Review", model = "review-model" });
        public async ValueTask DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "test")), "test")));
    }
}
