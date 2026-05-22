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

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 🤖 AGENTIC SYSTEM
// ============================================================================

builder.Services.AddAgenticSystemCore();
builder.Services.AddAgenticSystemInfrastructure(builder.Configuration);

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
        builder.Services.AddAGUI();
    }
}

// ============================================================================
// 🌐 WEB API — Controllers, Swagger, Auth, Rate Limiting
// ============================================================================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddSignalR(options =>
{
    options.AddFilter<AgenticSystem.Api.SignalR.TenantHubFilter>();
});

builder.Services.AddApiSecurity(builder.Configuration, builder.Environment);
builder.Services.AddApiSwagger();
builder.Services.AddApiRateLimiting(builder.Configuration);

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
    app.MapAGUI("AgenticSystem", "/agui")
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingServiceCollectionExtensions.ProtocolPolicyName);
}

// Seed built-in tools and skills
app.Services.SeedAgenticDefaults();
app.Services.SeedInfrastructureTools();

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
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Erro fatal ao aplicar migrações do PostgreSQL no startup.");
        }
    }
}

// Subscribe to FinOps events for real-time gateway monitoring
var eventBus = app.Services.GetRequiredService<AgenticSystem.Core.Interfaces.IEventBus>();
var gatewayHub = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<GatewayHub>>();
await eventBus.SubscribeAsync("FinOps.TurnCostUpdated", "GatewayHubPublisher", async busEvent =>
{
    if (busEvent.Payload.TryGetValue("TurnCostSummary", out var summaryObj))
    {
        await gatewayHub.Clients.All.SendAsync("TurnCostSummaryUpdated", summaryObj);
    }
});

await eventBus.SubscribeAsync("FinOps.QuotaThresholdReached", "GatewayHubPublisher", async busEvent =>
{
    await gatewayHub.Clients.All.SendAsync("QuotaThresholdReached", busEvent.Payload);
});

Log.Information("Agentic System starting up...");
app.Run();