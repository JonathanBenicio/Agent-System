using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { error = "A chave de API é obrigatória." });
        }

        var configuredKey = _configuration["AgenticSystem:AdminApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Admin API key não configurada no servidor." });
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(request.ApiKey.Trim()),
                Encoding.UTF8.GetBytes(configuredKey)))
        {
            return Unauthorized(new { error = "Chave de API inválida." });
        }

        Response.Cookies.Append("agentic_api_key", request.ApiKey.Trim(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Em dev ou prod, garante envio seguro
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        return Ok(new { success = true, role = "Admin" });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("agentic_api_key", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        return Ok(new { success = true });
    }
}

public record LoginRequest(string ApiKey);
