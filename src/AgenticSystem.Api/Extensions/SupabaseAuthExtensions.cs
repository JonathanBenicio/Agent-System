using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AgenticSystem.Api.Extensions;

public static class SupabaseAuthExtensions
{
    public static AuthenticationBuilder AddSupabaseAuth(this AuthenticationBuilder builder, IConfiguration configuration)
    {
        var supabaseSection = configuration.GetSection("Supabase");
        var jwtSecret = supabaseSection["JwtSecret"];
        
        // Supabase JWT is signed with a HS256 key (the project JWT secret)
        // or you can use JWKS if using a more complex setup.
        // For standard Supabase, it's a symmetric key.

        if (string.IsNullOrEmpty(jwtSecret))
        {
            return builder;
        }

        builder.AddJwtBearer("Supabase", options =>
        {
            options.Authority = supabaseSection["Url"]; // Optional, but good for validation
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = $"{supabaseSection["Url"]}/auth/v1",
                ValidateAudience = true,
                ValidAudience = "authenticated",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            // Support for SignalR
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        return builder;
    }
}
