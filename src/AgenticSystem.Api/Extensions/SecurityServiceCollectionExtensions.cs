using AgenticSystem.Api.Auth;
using Microsoft.AspNetCore.Authentication;

namespace AgenticSystem.Api.Extensions;

/// <summary>
/// Encapsulates all authentication and authorization registrations
/// (ApiKey, JWT Tenant, Supabase, MultiAuth PolicyScheme).
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var supabaseAuthenticationConfigured = !string.IsNullOrWhiteSpace(configuration["Supabase:JwtSecret"]);

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "MultiAuth";
        })
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.SchemeName, null)
        .AddScheme<JwtTenantAuthenticationOptions, JwtTenantAuthenticationHandler>(
            JwtTenantAuthenticationHandler.SchemeName, options =>
            {
                var jwtSection = configuration.GetSection("AgenticSystem:Jwt");
                var secretKey = jwtSection["SecretKey"];
                if (string.IsNullOrEmpty(secretKey) && !environment.IsDevelopment())
                    throw new InvalidOperationException("AgenticSystem:Jwt:SecretKey must be configured in non-Development environments.");
                options.SecretKey = environment.IsDevelopment() && string.IsNullOrEmpty(secretKey)
                    ? "default-dev-key-change-in-production-32chars!"
                    : secretKey!;
                options.Issuer = jwtSection["Issuer"] ?? "AgenticSystem";
                options.Audience = jwtSection["Audience"] ?? "AgenticSystem";
            })
        .AddSupabaseAuth(configuration)
        .AddPolicyScheme("MultiAuth", "ApiKey, JWT or Supabase", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                string? token = null;
                if (context.Request.Headers.TryGetValue("Authorization", out var authHeaderValue))
                {
                    var authHeader = authHeaderValue.ToString();
                    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = authHeader.Substring("Bearer ".Length).Trim();
                    }
                }

                if (string.IsNullOrEmpty(token) && context.Request.Query.TryGetValue("access_token", out var queryToken))
                {
                    token = queryToken.ToString();
                }

                if (!string.IsNullOrEmpty(token))
                {
                    try
                    {
                        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                        if (handler.CanReadToken(token))
                        {
                            var jwtToken = handler.ReadJwtToken(token);
                            if (string.Equals(jwtToken.Issuer, "AgenticSystem", StringComparison.OrdinalIgnoreCase))
                            {
                                return JwtTenantAuthenticationHandler.SchemeName;
                            }

                            return supabaseAuthenticationConfigured
                                ? "Supabase"
                                : JwtTenantAuthenticationHandler.SchemeName;
                        }
                    }
                    catch
                    {
                        // Opaque bearer API keys are handled by the ApiKey scheme below.
                    }
                    return ApiKeyAuthenticationHandler.SchemeName;
                }
                return ApiKeyAuthenticationHandler.SchemeName;
            };
        });

        services.AddAuthorization();

        return services;
    }
}
