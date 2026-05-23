using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Api.Controllers.OpenAI;

/// <summary>
/// Endpoint compatível com a API OpenAI /v1/chat/completions.
/// Expõe os agentes do Agentic System como um servidor OpenAI-compatible,
/// permitindo integração com ferramentas que consomem a API OpenAI
/// (Continue, Cursor, LangChain, etc.).
/// 
/// Autenticação via Bearer token (padrão OpenAI) — valida contra banco de dados.
/// </summary>
[ApiController]
[Route("v1")]
[EnableRateLimiting("ProtocolEndpoints")]
public class OpenAIChatCompletionController : ControllerBase
{
    private readonly IFrameworkOrchestratorService _orchestrator;
    private readonly AgenticDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly ILogger<OpenAIChatCompletionController> _logger;

    public OpenAIChatCompletionController(
        IFrameworkOrchestratorService orchestrator,
        AgenticDbContext dbContext,
        TenantContext tenantContext,
        ILogger<OpenAIChatCompletionController> logger)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Chat Completions — compatível com POST /v1/chat/completions da OpenAI.
    /// </summary>
    [HttpPost("chat/completions")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ChatCompletionResponse), 200)]
    [ProducesResponseType(typeof(ChatCompletionError), 401)]
    [ProducesResponseType(typeof(ChatCompletionError), 400)]
    [ProducesResponseType(typeof(ChatCompletionError), 500)]
    public async Task<IActionResult> CreateChatCompletion(
        [FromBody] ChatCompletionRequest request,
        CancellationToken ct)
    {
        // 1. Autenticação via Bearer token (padrão OpenAI)
        if (!await ValidateBearerTokenAsync())
        {
            return Unauthorized(new ChatCompletionError
            {
                Error = new ChatCompletionErrorDetail
                {
                    Message = "Invalid API key provided.",
                    Type = "invalid_request_error",
                    Code = "invalid_api_key"
                }
            });
        }

        // 2. Validação do request
        if (request.Messages is not { Count: > 0 })
        {
            return BadRequest(new ChatCompletionError
            {
                Error = new ChatCompletionErrorDetail
                {
                    Message = "messages is required and must contain at least one message.",
                    Type = "invalid_request_error",
                    Param = "messages"
                }
            });
        }

        if (request.Stream)
        {
            return BadRequest(new ChatCompletionError
            {
                Error = new ChatCompletionErrorDetail
                {
                    Message = "Streaming is not yet supported. Set stream to false.",
                    Type = "invalid_request_error",
                    Param = "stream"
                }
            });
        }

        // 3. Extrair último input do usuário
        var lastUserMessage = request.Messages
            .LastOrDefault(m => m.Role == "user");

        if (lastUserMessage?.Content is null or "")
        {
            return BadRequest(new ChatCompletionError
            {
                Error = new ChatCompletionErrorDetail
                {
                    Message = "At least one user message with content is required.",
                    Type = "invalid_request_error",
                    Param = "messages"
                }
            });
        }

        // 4. Gerar sessionId do user (ou usar o fornecido)
        var sessionId = request.User ?? $"openai-compat-{Guid.NewGuid():N}";

        _logger.LogInformation(
            "OpenAI-compatible request: model={Model}, messages={MessageCount}, session={Session}",
            request.Model, request.Messages.Count, sessionId);

        // 5. Executar via orquestrador do framework
        AgentResponse agentResponse;
        try
        {
            agentResponse = await _orchestrator.ExecuteAsync(
                sessionId,
                lastUserMessage.Content,
                new UserContext { UserId = sessionId, TenantId = _tenantContext.TenantId },
                ct);
        }
        catch (OperationCanceledException)
        {
            throw; // Let ASP.NET handle cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI-compatible endpoint: orchestrator execution failed");
            return StatusCode(500, new ChatCompletionError
            {
                Error = new ChatCompletionErrorDetail
                {
                    Message = "Internal server error during agent execution.",
                    Type = "server_error"
                }
            });
        }

        // 6. Converter para formato OpenAI
        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Estimativa simples de tokens (1 token ≈ 4 chars)
        var promptTokens = request.Messages.Sum(m => (m.Content?.Length ?? 0) / 4);
        var completionTokens = (agentResponse.Content?.Length ?? 0) / 4;

        var response = new ChatCompletionResponse
        {
            Id = completionId,
            Object = "chat.completion",
            Created = unixTimestamp,
            Model = request.Model,
            Choices = new List<ChatCompletionChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = agentResponse.Content ?? string.Empty
                    },
                    FinishReason = agentResponse.Success ? "stop" : "length"
                }
            },
            Usage = new ChatCompletionUsage
            {
                PromptTokens = Math.Max(1, promptTokens),
                CompletionTokens = Math.Max(1, completionTokens),
                TotalTokens = Math.Max(2, promptTokens + completionTokens)
            },
            SystemFingerprint = $"agentic-{agentResponse.AgentName}"
        };

        return Ok(response);
    }

    /// <summary>
    /// Models endpoint — lista modelos disponíveis (compatível com GET /v1/models).
    /// </summary>
    [HttpGet("models")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ListModels()
    {
        if (!await ValidateBearerTokenAsync())
        {
            return Unauthorized(new ChatCompletionError
            {
                Error = new ChatCompletionErrorDetail
                {
                    Message = "Invalid API key provided.",
                    Type = "invalid_request_error",
                    Code = "invalid_api_key"
                }
            });
        }

        var models = new
        {
            @object = "list",
            data = new[]
            {
                new
                {
                    id = "agentic-system",
                    @object = "model",
                    created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    owned_by = "agentic-system"
                }
            }
        };

        return Ok(models);
    }

    private async Task<bool> ValidateBearerTokenAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
            return false;

        // Suporta "Bearer <token>" (padrão OpenAI) e token direto
        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : authHeader.Trim();

        if (string.IsNullOrWhiteSpace(token))
            return false;

        // Calcula o Hash SHA-256 da chave fornecida
        var keyBytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(keyBytes);
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Consulta no banco de dados se o hash corresponde a uma chave ativa
        var accessKey = await _dbContext.AccessApiKeys
            .IgnoreQueryFilters() // Ignora o filtro de tenant
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsEnabled);

        if (accessKey is null)
            return false;

        // Popula o TenantContext scoped
        _tenantContext.TenantId = accessKey.TenantId;
        _tenantContext.TenantName = accessKey.Name;
        _tenantContext.IsAuthenticated = true;

        return true;
    }
}

