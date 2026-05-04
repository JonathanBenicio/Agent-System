using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Text.RegularExpressions;

namespace AgenticSystem.Api.Controllers;

/// <summary>
/// Endpoint voice-friendly para integração com Alexa, Google Assistant e similares.
/// Retorna texto limpo (sem markdown) e respeita timeout de 8s para skills de voz.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public partial class VoiceController : ControllerBase
{
    private readonly IMetaAgent _metaAgent;
    private readonly ILogger<VoiceController> _logger;
    private static readonly TimeSpan VoiceTimeout = TimeSpan.FromSeconds(7); // 1s de margem para Alexa (8s limit)

    public VoiceController(IMetaAgent metaAgent, ILogger<VoiceController> logger)
    {
        _metaAgent = metaAgent;
        _logger = logger;
    }

    /// <summary>
    /// Processa uma pergunta por voz e retorna resposta em texto limpo.
    /// Timeout de 7s para compatibilidade com Alexa (limite de 8s).
    /// </summary>
    [HttpPost("ask")]
    [ProducesResponseType(typeof(VoiceResponse), 200)]
    [ProducesResponseType(408)]
    public async Task<IActionResult> Ask([FromBody] VoiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new VoiceResponse("Nenhuma pergunta foi enviada.", false));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        cts.CancelAfter(VoiceTimeout);

        try
        {
            var userContext = new UserContext
            {
                UserId = request.UserId ?? "voice-user",
                Name = request.UserName ?? "Voice User",
                Language = request.Locale ?? "pt-BR"
            };

            var response = await _metaAgent.ProcessRequestAsync(request.Text, userContext);
            var cleanText = StripMarkdown(response?.Content ?? "Desculpe, não consegui processar sua pergunta.");

            _logger.LogInformation("🎙️ Voice request processed: {TextLength} chars", cleanText.Length);

            return Ok(new VoiceResponse(cleanText, true));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("⏰ Voice request timed out after {Timeout}s", VoiceTimeout.TotalSeconds);
            return StatusCode(408, new VoiceResponse("Desculpe, a resposta demorou demais. Tente perguntar de forma mais simples.", false));
        }
    }

    /// <summary>
    /// Remove formatação markdown para retornar texto limpo para TTS (text-to-speech).
    /// </summary>
    public static string StripMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Remove code blocks (```...```)
        text = CodeBlockRegex().Replace(text, "$1");
        // Remove inline code (`...`)
        text = InlineCodeRegex().Replace(text, "$1");
        // Remove bold/italic (**text**, *text*, __text__, _text_)
        text = BoldRegex().Replace(text, "$1");
        text = ItalicRegex().Replace(text, "$1");
        // Remove headers (# ... ##)
        text = HeaderRegex().Replace(text, "");
        // Remove images ![alt](url) — must be before links
        text = ImageRegex().Replace(text, "");
        // Remove links [text](url) → text
        text = LinkRegex().Replace(text, "$1");
        // Remove horizontal rules (---, ***)
        text = HorizontalRuleRegex().Replace(text, "");
        // Remove bullet points (- item, * item)
        text = BulletRegex().Replace(text, "");
        // Collapse multiple newlines
        text = MultiNewlineRegex().Replace(text, "\n");

        return text.Trim();
    }

    [GeneratedRegex(@"```[\w]*\n?(.*?)```", RegexOptions.Singleline)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*|__(.+?)__")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"\*(.+?)\*|_(.+?)_")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"\[([^\]]+)\]\([^\)]+\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"!\[[^\]]*\]\([^\)]+\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"^[-*]{3,}\s*$", RegexOptions.Multiline)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"^\s*[-*+]\s+", RegexOptions.Multiline)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiNewlineRegex();
}

public record VoiceRequest(
    string Text,
    string? UserId = null,
    string? UserName = null,
    string? Locale = null);

public record VoiceResponse(
    string Text,
    bool Success);
