using AgenticSystem.Api.Hubs;
using AgenticSystem.Api.Middleware;
using AgenticSystem.Core.Extensions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.AgentFramework;
using AgenticSystem.Infrastructure.Extensions;
using AgenticSystem.Api.Extensions;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using Serilog;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Agents.AI.DevUI;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// ============================================================================
// 🤖 AGENTIC SYSTEM
// ============================================================================

builder.Services.AddAgenticSystemCore();
builder.Services.AddAgenticSystemInfrastructure(builder.Configuration);
builder.Services.AddScoped<AgenticSystem.Api.Services.ChatConfigurationService>();
builder.Services.AddHttpContextAccessor();
// AG-UI hosting resolves this provider while MapAGUIServer builds routes from the root provider.
// The implementation is safe as a singleton because it reads request/tenant data from ambient accessors.
builder.Services.AddSingleton<Microsoft.Agents.AI.Hosting.AgentIsolationKeyProvider, AgenticSystem.Api.Auth.TenantSessionIsolationKeyProvider>();

// Register SignalR-based session event publisher for real-time UI sync
builder.Services.AddSingleton<AgenticSystem.Core.Interfaces.IEventPublisher, AgenticSystem.Api.SignalR.SignalRSessionEventPublisher>();

// Register SignalR-based workflow event broadcaster
builder.Services.AddSingleton<AgenticSystem.Core.Interfaces.IWorkflowEventBroadcaster, AgenticSystem.Api.Hubs.SignalRWorkflowEventBroadcaster>();

// Register SignalR-based ONNX event broadcaster
builder.Services.AddSingleton<AgenticSystem.Core.Interfaces.IOnnxEventBroadcaster, AgenticSystem.Api.Hubs.SignalROnnxEventBroadcaster>();

builder.Services.UseLocalExecutionStorageMode(builder.Configuration);

// ============================================================================
// 🔌 PROTOCOL HOSTING — A2A + AG-UI
// ============================================================================

var protocolHosting = builder.Configuration.GetSection("ProtocolHosting");
var a2aEnabled = protocolHosting.GetValue<bool>("A2A:Enabled");
var agUiEnabled = protocolHosting.GetValue<bool>("AgUI:Enabled");

if (a2aEnabled || agUiEnabled)
{
    builder.Services.AddKeyedSingleton<Microsoft.Agents.AI.AIAgent>("AgenticSystem", (sp, key) =>
    {
        var metadata = sp.GetRequiredService<OrchestratorMetadata>();
        return new ScopedAgentProxy(
            rootServiceProvider: sp,
            targetAgentKey: metadata.Name,
            name: "AgenticSystem",
            description: "Agentic System Protocol Proxy"
        );
    });

    if (a2aEnabled)
    {
        builder.Services.AddA2AServer("AgenticSystem");
    }

    if (agUiEnabled)
    {
        builder.Services.AddAGUIServer();
    }
}

// ============================================================================
// 🌐 WEB API — Controllers, Swagger, Auth, Rate Limiting
// ============================================================================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddScoped<AgenticSystem.Api.SignalR.TenantHubFilter>();
builder.Services.AddSignalR(options =>
{
    options.AddFilter<AgenticSystem.Api.SignalR.TenantHubFilter>();
});
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, AgenticSystem.Api.SignalR.AgenticUserIdProvider>();

builder.Services.AddApiSecurity(builder.Configuration, builder.Environment);
builder.Services.AddApiSwagger();
builder.Services.AddApiRateLimiting(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevUI();
    builder.Services.Configure<DevUIOptions>(options =>
    {
        options.AllowRemoteAccess = true;
    });
    builder.Services.AddOpenAIResponses();
    builder.Services.AddOpenAIConversations();
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            var allowedOrigins = builder.Configuration.GetSection("AgenticSystem:Cors:AllowedOrigins")
                .Get<string[]>() ?? throw new InvalidOperationException("CORS AllowedOrigins must be configured in Production.");
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// ============================================================================
// 📊 OBSERVABILITY (Telemetry & Logging)
// ============================================================================

builder.AddEnterpriseObservability("AgenticSystem.Api");

// ============================================================================
// 🏗️ BUILD & CONFIGURE PIPELINE
// ============================================================================

var app = builder.Build();

// This service exists only in the isolated validation host so that the
// GatewayHub tenant-broadcast path can be exercised over a real SignalR socket.
if (app.Environment.IsEnvironment("Validation"))
{
    var tenantContextAccessor = app.Services.GetRequiredService<AgenticSystem.Core.Interfaces.ITenantContextAccessor>();
    using (tenantContextAccessor.BeginScope(new AgenticSystem.Core.Models.TenantContext
    {
        TenantId = "system-validation-gateway",
        TenantName = "Validation Gateway Fixture"
    }))
    {
        app.Services.GetRequiredService<AgenticSystem.Core.Interfaces.IServiceGateway>()
            .RegisterService(new AgenticSystem.Core.Models.ServiceRegistration
            {
                Name = "validation-gateway-fixture",
                Category = "validation",
                DailyBudget = 1m
            });
    }
}

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetService<AgenticSystem.Infrastructure.Persistence.AgenticDbContext>();
    if (dbContext is not null)
    {
        try
        {
            Serilog.Log.Information("Executando migrações do PostgreSQL/Supabase no startup...");
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(dbContext.Database);
            Serilog.Log.Information("Migrações concluídas com sucesso.");

            var bootstrapService = scope.ServiceProvider.GetService<AgenticSystem.Core.Interfaces.ISystemBootstrapService>();
            if (bootstrapService is not null)
            {
                Serilog.Log.Information("Executando auto-bootstrap do banco de dados...");
                await bootstrapService.BootstrapAsync();
                Serilog.Log.Information("Auto-bootstrap finalizado.");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Erro fatal ao aplicar migrações ou auto-bootstrap no startup.");
        }
    }
}

app.UseExceptionHandler(exApp =>
{
    exApp.Run(async context =>
    {
        var correlationId = context.TraceIdentifier;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        Log.Error("Unhandled exception - CorrelationId: {CorrelationId}", correlationId);
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Ocorreu um erro interno. Tente novamente mais tarde.",
            correlationId
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Agentic System API v1"));

    var developmentTenants = await app.Services.GetRequiredService<ITenantStore>().GetAllAsync();
    var developmentTenant = developmentTenants.FirstOrDefault(tenant => tenant.IsActive);
    if (developmentTenant is not null)
    {
        using var tenantScope = app.Services.GetRequiredService<ITenantContextAccessor>()
            .BeginScope(new AgenticSystem.Core.Models.TenantContext { TenantId = developmentTenant.Id, TenantName = developmentTenant.Name });
        var agent = app.Services.GetRequiredKeyedService<Microsoft.Agents.AI.AIAgent>("AgenticSystem");
        app.MapOpenAIResponses(agent);
        app.MapOpenAIConversations();
        app.MapDevUI();
    }
    else
        Log.Information("Development agent endpoints are not mapped until a real tenant is provisioned.");
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseTenantMiddleware();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

// SignalR Hubs
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();
app.MapHub<GatewayHub>("/hubs/gateway").RequireAuthorization();
app.MapHub<ExternalAgentHub>("/hubs/external-agent").RequireAuthorization();
app.MapHub<WorkflowHub>("/hubs/workflow").RequireAuthorization();
app.MapHub<OnnxHub>("/hubs/onnx").RequireAuthorization();

// Health & Version
app.MapMethods("/health", new[] { "GET", "HEAD" }, () => new { Status = "Healthy", Timestamp = DateTime.UtcNow }).AllowAnonymous();
app.MapGet("/version", () => new { Version = "1.0.0", Build = DateTime.UtcNow.ToString("yyyyMMdd-HHmm") });

// Protocol hosting endpoints — A2A + AG-UI
if (a2aEnabled)
{
    app.MapA2AHttpJson("AgenticSystem", "/a2a")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingServiceCollectionExtensions.ProtocolPolicyName);
}
if (agUiEnabled)
{
    app.MapAGUIServer("AgenticSystem", "/agui")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingServiceCollectionExtensions.ProtocolPolicyName);
}

// Seed built-in tools and skills
app.Services.SeedAgenticDefaults();
app.Services.SeedInfrastructureTools();



// Subscribe to FinOps events for real-time gateway monitoring
var eventBus = app.Services.GetRequiredService<AgenticSystem.Core.Interfaces.IEventBus>();
var gatewayHub = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<GatewayHub>>();
await eventBus.SubscribeAsync("FinOps.TurnCostUpdated", "GatewayHubPublisher", async busEvent =>
{
    if (busEvent.Payload.TryGetValue("TurnCostSummary", out var summaryObj))
    {
        await gatewayHub.Clients.Group($"tenant:{busEvent.TenantId}:gateway").SendAsync("TurnCostSummaryUpdated", summaryObj);
    }
});

await eventBus.SubscribeAsync("FinOps.QuotaThresholdReached", "GatewayHubPublisher", async busEvent =>
{
    await gatewayHub.Clients.Group($"tenant:{busEvent.TenantId}:gateway").SendAsync("QuotaThresholdReached", busEvent.Payload);
});

Log.Information("Agentic System starting up...");
app.Run();
