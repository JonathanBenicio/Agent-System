using System.Text.Json.Serialization;

namespace AgenticSystem.Api.Extensions;

/// <summary>
/// Encapsulates Swagger/OpenAPI registration and schema definitions.
/// </summary>
public static class SwaggerServiceCollectionExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
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

        return services;
    }
}
