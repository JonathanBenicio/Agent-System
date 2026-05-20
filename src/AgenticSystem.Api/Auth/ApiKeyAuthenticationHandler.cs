using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Api.Auth;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? providedKey = null;

        if (Request.Headers.TryGetValue(HeaderName, out var apiKeyValues))
        {
            providedKey = apiKeyValues.FirstOrDefault()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(providedKey) && Request.Cookies.TryGetValue("agentic_api_key", out var cookieKey))
        {
            providedKey = cookieKey?.Trim();
        }

        if (string.IsNullOrWhiteSpace(providedKey) && Request.Path.StartsWithSegments("/hubs"))
        {
            if (Request.Query.TryGetValue("api_key", out var queryKey) || Request.Query.TryGetValue("X-Api-Key", out queryKey))
            {
                providedKey = queryKey.FirstOrDefault()?.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(providedKey))
        {
            if (Context.Request.Path.StartsWithSegments("/health"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Api-Key header or query parameter."));
        }
        var configuredKey = _configuration["AgenticSystem:AdminApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
            return Task.FromResult(AuthenticateResult.Fail("Admin API key not configured on server."));

        if (string.IsNullOrWhiteSpace(providedKey) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedKey),
                Encoding.UTF8.GetBytes(configuredKey)))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.NameIdentifier, "admin"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("tenant_id", Core.Models.Tenant.DefaultTenantId)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
