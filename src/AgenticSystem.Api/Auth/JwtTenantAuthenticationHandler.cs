using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgenticSystem.Api.Auth;

/// <summary>
/// Authentication handler para JWT Bearer tokens com claim tenant_id.
/// Usado em conjunto com ApiKeyAuthenticationHandler (dual auth).
/// </summary>
public class JwtTenantAuthenticationHandler : AuthenticationHandler<JwtTenantAuthenticationOptions>
{
    public const string SchemeName = "JwtBearer";

    public JwtTenantAuthenticationHandler(
        IOptionsMonitor<JwtTenantAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var headerValue = authHeader.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue) || !headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = headerValue["Bearer ".Length..].Trim();

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(Options.SecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = !string.IsNullOrEmpty(Options.Issuer),
                ValidIssuer = Options.Issuer,
                ValidateAudience = !string.IsNullOrEmpty(Options.Audience),
                ValidAudience = Options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

            // Garante que o claim tenant_id existe
            var tenantClaim = principal.FindFirst("tenant_id");
            if (tenantClaim is null)
            {
                return Task.FromResult(AuthenticateResult.Fail("Token missing tenant_id claim."));
            }

            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (SecurityTokenExpiredException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Token expired."));
        }
        catch (SecurityTokenException ex)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Token validation failed: {ex.Message}"));
        }
    }
}

public class JwtTenantAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Chave secreta para validação do JWT.
    /// Config key: AgenticSystem:Jwt:SecretKey
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    public string? Issuer { get; set; }
    public string? Audience { get; set; }
}
