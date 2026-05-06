using AgenticSystem.Api.Auth;
using AgenticSystem.Api.Hubs;
using AgenticSystem.Api.Middleware;
using AgenticSystem.Api.MCP;
using AgenticSystem.Core.Extensions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Extensions;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.AI;
using ModelContextProtocol.AspNetCore;
using Serilog;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 🧠 LLM — Microsoft.Extensions.AI
// ============================================================================

// IChatClient is registered in AddAgenticSystemInfrastructure via OpenAI SDK.
// Pipeline middleware (logging, telemetry) is applied automatically.

// ============================================================================
// 🤖 AGENTIC SYSTEM
// ============================================================================

builder.Services.AddAgenticSystemCore();
builder.Services.AddAgenticSystemInfrastructure(builder.Configuration);
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
    })
    .WithTools<AgenticMcpTools>();

builder.Services.UseLocalExecutionStorageMode(builder.Configuration);

// ============================================================================
// 🔌 PROTOCOL HOSTING — A2A + AG-UI
// ============================================================================

var protocolHosting = builder.Configuration.GetSection("ProtocolHosting");
var a2aEnabled = protocolHosting.GetValue<bool>("A2A:Enabled");
var agUiEnabled = protocolHosting.GetValue<bool>("AgUI:Enabled");
var protocolRateLimitingEnabled = protocolHosting.GetValue("RateLimiting:Enabled", true);
var protocolRateLimitPermitLimit = protocolHosting.GetValue("RateLimiting:PermitLimit", 60);
var protocolRateLimitWindowSeconds = protocolHosting.GetValue("RateLimiting:WindowSeconds", 60);
var protocolRateLimitQueueLimit = protocolHosting.GetValue("RateLimiting:QueueLimit", 0);

if (a2aEnabled || agUiEnabled)
{
    // Protocol-facing agent uses the full orchestrator pipeline via keyed IChatClient.
    // O ProtocolOrchestratorChatClient delega para IFrameworkOrchestratorService.ExecuteAsync,
    // garantindo acesso a tools, RAG, especialistas e middleware (Finding 11 resolved).
    builder.Services.AddAIAgent(
        "AgenticSystem",
        "You are the Agentic System orchestrator. Route requests to the appropriate specialist agent.",
        "Multi-agent orchestrator for A2A and AG-UI protocol interoperability",
        chatClientServiceKey: "protocol-orchestrator");

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
// 🌐 WEB API
// ============================================================================

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Agentic System API",
        Version = "v1",
        Description = "Sistema Agentic Generalista com Meta-Agent dinamico"
    });
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        Description = "Admin API Key"
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT token com claim tenant_id"
    });
    options.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("ApiKey", doc),
            Array.Empty<string>().ToList()
        }
    });
});

builder.Services.AddSignalR();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "MultiAuth";
})
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationHandler.SchemeName, null)
.AddScheme<JwtTenantAuthenticationOptions, JwtTenantAuthenticationHandler>(
    JwtTenantAuthenticationHandler.SchemeName, options =>
    {
        var jwtSection = builder.Configuration.GetSection("AgenticSystem:Jwt");
        var secretKey = jwtSection["SecretKey"];
        if (string.IsNullOrEmpty(secretKey) && !builder.Environment.IsDevelopment())
            throw new InvalidOperationException("AgenticSystem:Jwt:SecretKey must be configured in non-Development environments.");
        options.SecretKey = builder.Environment.IsDevelopment() && string.IsNullOrEmpty(secretKey)
            ? "default-dev-key-change-in-production-32chars!"
            : secretKey!;
        options.Issuer = jwtSection["Issuer"] ?? "AgenticSystem";
        options.Audience = jwtSection["Audience"] ?? "AgenticSystem";
    })
.AddPolicyScheme("MultiAuth", "ApiKey or JWT", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        if (context.Request.Headers.ContainsKey("Authorization"))
            return JwtTenantAuthenticationHandler.SchemeName;
        return ApiKeyAuthenticationHandler.SchemeName;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = protocolRateLimitWindowSeconds.ToString();
        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Protocol rate limit exceeded. Try again later."
            }, cancellationToken: ct);
        }
    };

    options.AddPolicy(ProtocolRateLimiting.PolicyName, httpContext =>
    {
        if (!protocolRateLimitingEnabled)
        {
            return RateLimitPartition.GetNoLimiter("protocol-rate-limiting-disabled");
        }

        var partitionKey = ProtocolRateLimiting.BuildPartitionKey(httpContext);
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = protocolRateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(protocolRateLimitWindowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = protocolRateLimitQueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

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
// 📊 LOGGING
// ============================================================================

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithProperty("ApplicationName", "AgenticSystem")
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/agentic-system-.log", rollingInterval: RollingInterval.Day);
});

// ============================================================================
// 🏗️ BUILD & CONFIGURE PIPELINE
// ============================================================================

// Chat endpoint rate limiter - sliding window per tenant
var ChatRateLimiter = new ConcurrentDictionary<string, ConcurrentQueue<DateTime>>();

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

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseTenantMiddleware();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();
app.MapHub<GatewayHub>("/hubs/gateway").RequireAuthorization();

app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });
app.MapGet("/version", () => new { Version = "1.0.0", Build = DateTime.UtcNow.ToString("yyyyMMdd-HHmm") });

// Protocol hosting endpoints — A2A + AG-UI
if (a2aEnabled)
{
    app.MapA2AHttpJson("AgenticSystem", "/a2a")
        .RequireAuthorization()
        .RequireRateLimiting(ProtocolRateLimiting.PolicyName);
}
if (agUiEnabled)
{
    app.MapAGUI("AgenticSystem", "/agui")
        .RequireAuthorization()
        .RequireRateLimiting(ProtocolRateLimiting.PolicyName);
}

app.MapPost("/api/chat", async (ChatRequest request, IMetaAgent metaAgent, TenantContext tenantContext, HttpContext httpContext) =>
{
    // Rate limiting per tenant (sliding window)
    var tenantKey = tenantContext.TenantId ?? "default";
    var now = DateTime.UtcNow;
    var window = ChatRateLimiter.GetOrAdd(tenantKey, _ => new ConcurrentQueue<DateTime>());

    // Prune entries older than 1 minute
    while (window.TryPeek(out var oldest) && (now - oldest).TotalSeconds > 60)
        window.TryDequeue(out _);

    var maxPerMinute = tenantContext.Limits?.MaxRequestsPerMinute ?? 30;
    if (window.Count >= maxPerMinute)
        return Results.Json(new { error = "Rate limit exceeded. Try again later." }, statusCode: StatusCodes.Status429TooManyRequests);

    window.Enqueue(now);

    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Message is required." });

    if (request.Message.Length > 10_000)
        return Results.BadRequest(new { error = "Message exceeds maximum length of 10000 characters." });

    // Identity from authenticated principal - never trust client-supplied userId
    var authenticatedUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? httpContext.User.FindFirst("sub")?.Value
        ?? httpContext.User.Identity?.Name
        ?? "authenticated-user";

    var userContext = new UserContext
    {
        UserId = authenticatedUserId,
        Name = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? request.UserName ?? "User",
        TenantId = tenantContext.TenantId ?? Tenant.DefaultTenantId,
        Language = "pt-BR",
        Preferences = ChatRequestPreferencesBuilder.BuildLlmPreferences(request)
    };

    AgentResponse response;
    if (!string.IsNullOrWhiteSpace(request.TargetAgent))
    {
        response = await metaAgent.ProcessDirectRequestAsync(request.Message, userContext, request.TargetAgent);
    }
    else
    {
        response = await metaAgent.ProcessRequestAsync(request.Message, userContext);
    }

    return Results.Ok(response);
}).RequireAuthorization();

app.MapPost("/api/chat/stream", async (ChatRequest request, IMetaAgent metaAgent, TenantContext tenantContext, HttpContext httpContext) =>
{
    var tenantKey = tenantContext.TenantId ?? "default";
    var now = DateTime.UtcNow;
    var window = ChatRateLimiter.GetOrAdd(tenantKey, _ => new ConcurrentQueue<DateTime>());

    while (window.TryPeek(out var oldest) && (now - oldest).TotalSeconds > 60)
        window.TryDequeue(out _);

    var maxPerMinute = tenantContext.Limits?.MaxRequestsPerMinute ?? 30;
    if (window.Count >= maxPerMinute)
        return Results.Json(new { error = "Rate limit exceeded. Try again later." }, statusCode: StatusCodes.Status429TooManyRequests);

    window.Enqueue(now);

    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Message is required." });

    if (request.Message.Length > 10_000)
        return Results.BadRequest(new { error = "Message exceeds maximum length of 10000 characters." });

    var authenticatedUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? httpContext.User.FindFirst("sub")?.Value
        ?? httpContext.User.Identity?.Name
        ?? "authenticated-user";

    var userContext = new UserContext
    {
        UserId = authenticatedUserId,
        Name = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? request.UserName ?? "User",
        TenantId = tenantContext.TenantId ?? Tenant.DefaultTenantId,
        Language = "pt-BR",
        Preferences = ChatRequestPreferencesBuilder.BuildLlmPreferences(request)
    };

    httpContext.Response.StatusCode = StatusCodes.Status200OK;
    httpContext.Response.Headers.Append("Cache-Control", "no-cache");
    httpContext.Response.Headers.Append("X-Accel-Buffering", "no");
    httpContext.Response.ContentType = "text/event-stream";

    var stream = !string.IsNullOrWhiteSpace(request.TargetAgent)
        ? metaAgent.ProcessDirectRequestStreamAsync(request.Message, userContext, request.TargetAgent, httpContext.RequestAborted)
        : metaAgent.ProcessRequestStreamAsync(request.Message, userContext, httpContext.RequestAborted);

    await foreach (var streamEvent in stream.WithCancellation(httpContext.RequestAborted))
    {
        await SseWriter.WriteSseEventAsync(httpContext, streamEvent, httpContext.RequestAborted);
    }

    return Results.Empty;
}).RequireAuthorization();

// Seed built-in tools and skills
app.Services.SeedAgenticDefaults();
app.Services.SeedInfrastructureTools();

Log.Information("Agentic System starting up...");
app.Run();

// ============================================================================
// 📝 REQUEST/RESPONSE MODELS
// ============================================================================

public record ChatRequest(
    string Message,
    string? UserId = null,
    string? UserName = null,
    string? TargetAgent = null,
    string? Provider = null,
    string? Model = null,
    string? ApiKey = null,
    Dictionary<string, object>? Context = null);

static class ChatRequestPreferencesBuilder
{
    public static Dictionary<string, object> BuildLlmPreferences(ChatRequest request)
    {
        var preferences = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (request.Context is not null)
        {
            foreach (var kv in request.Context)
            {
                preferences[kv.Key] = kv.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            preferences["llm.request.provider"] = request.Provider;
            preferences["llm.session.provider"] = request.Provider;
            preferences["llm.provider"] = request.Provider;
        }

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            preferences["llm.request.model"] = request.Model;
            preferences["llm.session.model"] = request.Model;
            preferences["llm.model"] = request.Model;
        }

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            preferences["llm.request.apiKey"] = request.ApiKey;
            preferences["llm.session.apiKey"] = request.ApiKey;
            preferences["llm.apiKey"] = request.ApiKey;
        }

        return preferences;
    }
}

public record ChatResponse(
    string Response,
    string AgentUsed,
    int AgentTier,
    List<string> ActionsPerformed,
    Dictionary<string, object>? Metadata = null);

static class SseWriter
{
    public static async Task WriteSseEventAsync(HttpContext httpContext, AgentStreamEvent streamEvent, CancellationToken ct)
    {
        var eventName = ToSseEventName(streamEvent.Type);
        var json = JsonSerializer.Serialize(streamEvent);
        await httpContext.Response.WriteAsync($"event: {eventName}\n", ct);
        await httpContext.Response.WriteAsync($"data: {json}\n\n", ct);
        await httpContext.Response.Body.FlushAsync(ct);
    }

    private static string ToSseEventName(AgentStreamEventType type)
        => type.ToString().ToLowerInvariant();
}

static class ProtocolRateLimiting
{
    public const string PolicyName = "ProtocolEndpoints";

    public static string BuildPartitionKey(HttpContext httpContext)
    {
        var tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.Identity?.Name;

        if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(userId))
        {
            return $"tenant:{tenantId}:user:{userId}";
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authorization)
            && !string.IsNullOrWhiteSpace(authorization))
        {
            return $"auth:{Hash(authorization.ToString())}";
        }

        if (httpContext.Request.Headers.TryGetValue("X-Api-Key", out var apiKey)
            && !string.IsNullOrWhiteSpace(apiKey))
        {
            return $"api-key:{Hash(apiKey.ToString())}";
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remoteIp) ? "anonymous" : $"ip:{remoteIp}";
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..8]);
    }
}