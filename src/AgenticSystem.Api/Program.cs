using AgenticSystem.Api.Auth;
using AgenticSystem.Api.Hubs;
using AgenticSystem.Api.Middleware;
using AgenticSystem.Core.Extensions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.SemanticKernel;
using Serilog;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════
// 🧠 SEMANTIC KERNEL
// ═══════════════════════════════════════════════════════════

var kernelBuilder = builder.Services.AddKernel();

var azureOpenAI = builder.Configuration.GetSection("AzureOpenAI");
if (azureOpenAI.Exists() && !string.IsNullOrEmpty(azureOpenAI["Endpoint"]))
{
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: azureOpenAI["DeploymentName"]!,
        endpoint: azureOpenAI["Endpoint"]!,
        apiKey: azureOpenAI["ApiKey"]!,
        serviceId: "chat");
}

// ═══════════════════════════════════════════════════════════
// 🤖 AGENTIC SYSTEM
// ═══════════════════════════════════════════════════════════

builder.Services.AddAgenticSystemCore();
builder.Services.AddAgenticSystemInfrastructure(builder.Configuration);

// ─── Session Store: PostgreSQL em produção, InMemory em dev/test ───
var pgConn = builder.Configuration.GetConnectionString("SessionStore");
if (!string.IsNullOrWhiteSpace(pgConn))
    builder.Services.UsePostgresSessionStore(pgConn);

// ═══════════════════════════════════════════════════════════
// 🌐 WEB API
// ═══════════════════════════════════════════════════════════

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
        Description = "Sistema Agentic Generalista com Meta-Agent dinâmico"
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
        options.SecretKey = secretKey ?? "default-dev-key-change-in-production-32chars!";
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

// ═══════════════════════════════════════════════════════════
// 📊 LOGGING
// ═══════════════════════════════════════════════════════════

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithProperty("ApplicationName", "AgenticSystem")
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/agentic-system-.log", rollingInterval: RollingInterval.Day);
});

// ═══════════════════════════════════════════════════════════
// 🏗️ BUILD & CONFIGURE PIPELINE
// ═══════════════════════════════════════════════════════════

var app = builder.Build();

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
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<GatewayHub>("/hubs/gateway");

app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });
app.MapGet("/version", () => new { Version = "1.0.0", Build = DateTime.UtcNow.ToString("yyyyMMdd-HHmm") });

app.MapPost("/api/chat", async (ChatRequest request, IMetaAgent metaAgent, TenantContext tenantContext) =>
{
    var userContext = new UserContext
    {
        UserId = request.UserId ?? "anonymous",
        Name = request.UserName ?? "User",
        TenantId = tenantContext.TenantId,
        Language = "pt-BR"
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
});

// Seed built-in tools and skills
app.Services.SeedAgenticDefaults();
app.Services.SeedInfrastructureTools();

Log.Information("🤖 Agentic System starting up...");
app.Run();

// ═══════════════════════════════════════════════════════════
// 📝 REQUEST/RESPONSE MODELS
// ═══════════════════════════════════════════════════════════

public record ChatRequest(
    string Message,
    string? UserId = null,
    string? UserName = null,
    string? TargetAgent = null,
    Dictionary<string, object>? Context = null);

public record ChatResponse(
    string Response,
    string AgentUsed,
    int AgentTier,
    List<string> ActionsPerformed,
    Dictionary<string, object>? Metadata = null);