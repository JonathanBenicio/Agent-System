
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Agents;

public class NotificationAgent : BaseAgent
{
    public NotificationAgent(
        ISkillManager skillManager,
        ILogger<NotificationAgent> logger,
        IAgentMemoryService? agentMemoryService = null)
        : base(skillManager, logger, agentMemoryService) { }

    public override string Name => "NotificationAgent";
    public override string Description => "Triagem e priorização de notificações, alertas e comunicações.";
    public override AgentTier Tier => AgentTier.Support;
    public override string Domain => "notification";
    public override IEnumerable<string> AvailableTools => new[] { "email", "slack", "push" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um assistente de comunicações. Ajuda com:
        - Triagem e priorização de notificações
        - Resumo de emails e mensagens
        - Roteamento de alertas por urgência
        - Redação de respostas rápidas
        Priorize por urgência e relevância. Minimize distrações.
        """;
}

public class APIAgent : BaseAgent
{
    public APIAgent(
        ISkillManager skillManager,
        ILogger<APIAgent> logger,
        IAgentMemoryService? agentMemoryService = null)
        : base(skillManager, logger, agentMemoryService) { }

    public override string Name => "APIAgent";
    public override string Description => "Integração com serviços externos, APIs REST, webhooks e automação.";
    public override AgentTier Tier => AgentTier.Support;
    public override string Domain => "api";
    public override IEnumerable<string> AvailableTools => new[] { "http", "webhook", "graphql" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um especialista em integrações. Ajuda com:
        - Chamadas a APIs REST e GraphQL
        - Configuração de webhooks
        - Automação de workflows entre sistemas
        - Troubleshooting de integrações
        Priorize segurança: valide inputs, use HTTPS, não exponha secrets.
        """;
}

public class GeneralAgent : BaseAgent
{
    public GeneralAgent(
        ISkillManager skillManager,
        ILogger<GeneralAgent> logger,
        IAgentMemoryService? agentMemoryService = null)
        : base(skillManager, logger, agentMemoryService) { }

    public override string Name => "GeneralAgent";
    public override string Description => "Fallback para solicitações que não se encaixam em nenhum domínio específico.";
    public override AgentTier Tier => AgentTier.Support;
    public override string Domain => "general";

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um assistente geral versátil. Responda de forma útil e direta.
        Se identificar que a solicitação pertence a um domínio específico,
        sugira o handoff para o agent especializado.
        """;
}
