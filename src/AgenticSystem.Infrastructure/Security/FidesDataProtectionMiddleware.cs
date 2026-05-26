using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using FrameworkAgentResponse = Microsoft.Agents.AI.AgentResponse;

namespace AgenticSystem.Infrastructure.Security;

/// <summary>
/// Middleware do Agent Framework que executa o FIDES (proteção de dados sensíveis).
/// Intercepta RunCoreAsync para mascarar dados sensíveis antes de enviá-los ao LLM.
/// Registrado no pipeline via .UseFidesDataProtection() extension method.
/// </summary>
public class FidesDataProtectionMiddleware : DelegatingAIAgent
{
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ILogger _logger;

    // Regras padrão de mascaramento
    private static readonly List<(string Name, Regex Pattern, string Mask)> DefaultRules = new()
    {
        ("CPF", new Regex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b", RegexOptions.Compiled), "[CPF MASCARADO]"),
        ("CreditCard", new Regex(@"\b(?:\d{4}[ -]?){3}\d{4}\b", RegexOptions.Compiled), "[CARTÃO MASCARADO]"),
        ("Email", new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "[EMAIL MASCARADO]"),
        ("Token", new Regex(@"\b(?:sk-[a-zA-Z0-9]{32,}|eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+)\b", RegexOptions.Compiled), "[TOKEN MASCARADO]")
    };

    public FidesDataProtectionMiddleware(
        AIAgent innerAgent,
        ITenantContextAccessor tenantContext,
        ILogger logger)
        : base(innerAgent)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<FrameworkAgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        // Mascara as mensagens antes de processar
        var maskedMessages = MaskMessages(messages.ToList());

        // Delegate to inner agent
        return await base.RunCoreAsync(maskedMessages, session, options, cancellationToken);
    }

    private IEnumerable<ChatMessage> MaskMessages(IList<ChatMessage> originalMessages)
    {
        var maskedMessages = new List<ChatMessage>(originalMessages.Count);
        bool anyMasked = false;

        foreach (var msg in originalMessages)
        {
            if (msg.Role != ChatRole.User && msg.Role != ChatRole.System)
            {
                maskedMessages.Add(msg);
                continue;
            }

            var maskedMsg = new ChatMessage(msg.Role, string.Empty);
            foreach (var content in msg.Contents)
            {
                if (content is TextContent textContent)
                {
                    var maskedText = ApplyFidesMasks(textContent.Text, out bool hasMasked);
                    if (hasMasked)
                    {
                        anyMasked = true;
                    }
                    maskedMsg.Contents.Add(new TextContent(maskedText));
                }
                else
                {
                    maskedMsg.Contents.Add(content);
                }
            }
            maskedMessages.Add(maskedMsg);
        }

        if (anyMasked)
        {
            _logger.LogInformation("🛡️ FIDES: Dados sensíveis interceptados e mascarados para o Tenant {TenantId}", _tenantContext.CurrentTenantId);
        }

        return maskedMessages;
    }

    private string ApplyFidesMasks(string input, out bool anyMasked)
    {
        anyMasked = false;
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = input;
        foreach (var rule in DefaultRules)
        {
            if (rule.Pattern.IsMatch(result))
            {
                anyMasked = true;
                result = rule.Pattern.Replace(result, rule.Mask);
            }
        }
        return result;
    }
}
