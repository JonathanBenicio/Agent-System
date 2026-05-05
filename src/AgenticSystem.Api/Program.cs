using AgenticSystem.Api.Auth;
using AgenticSystem.Api.Hubs;
using AgenticSystem.Api.Middleware;
using AgenticSystem.Api.MCP;
using AgenticSystem.Core.Extensions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.AI;
using ModelContextProtocol.AspNetCore;
using Serilog;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "Admin API Key"
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT token com claim tenant_id"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
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
app.UseAuthorization();
app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();
app.MapHub<GatewayHub>("/hubs/gateway").RequireAuthorization();

app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });
app.MapGet("/version", () => new { Version = "1.0.0", Build = DateTime.UtcNow.ToString("yyyyMMdd-HHmm") });

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