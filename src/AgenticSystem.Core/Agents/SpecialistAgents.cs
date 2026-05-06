using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Agents;

public class CalendarAgent : BaseAgent
{
    public CalendarAgent(
        IChatClient chatClient,
        ISkillManager skillManager,
        ILogger<CalendarAgent> logger,
        IAgentMemoryService? agentMemoryService = null)
        : base(chatClient, skillManager, logger, agentMemoryService) { }

    public override string Name => "CalendarAgent";
    public override string Description => "Gerencia eventos, agendamentos, compromissos e disponibilidade.";
    public override AgentTier Tier => AgentTier.Specialist;
    public override string Domain => "calendar";
    public override IEnumerable<string> AvailableTools => new[] { "calendar", "email" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um especialista em gestão de agenda e tempo. Ajuda com:
        - Criação e gerenciamento de eventos
        - Verificação de disponibilidade
        - Agendamento inteligente evitando conflitos
        - Lembretes e recorrências
        Considere fusos horários e preferências do usuário.
        """;
}

public class CreativeAgent : BaseAgent
{
    public CreativeAgent(
        IChatClient chatClient,
        ISkillManager skillManager,
        ILogger<CreativeAgent> logger,
        IAgentMemoryService? agentMemoryService = null)
        : base(chatClient, skillManager, logger, agentMemoryService) { }

    public override string Name => "CreativeAgent";
    public override string Description => "Brainstorming, geração de conteúdo, escrita criativa e ideação.";
    public override AgentTier Tier => AgentTier.Specialist;
    public override string Domain => "creative";
    public override IEnumerable<string> AvailableTools => new[] { "notes", "search", "image" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um parceiro criativo versátil. Ajuda com:
        - Brainstorming e geração de ideias
        - Escrita criativa e conteúdo
        - Naming e copywriting
        - Visualização de conceitos
        Seja inventivo e proponha alternativas inesperadas.
        """;
}

public class AnalysisAgent : BaseAgent
{
    public AnalysisAgent(
        IChatClient chatClient,
        ISkillManager skillManager,
        ILogger<AnalysisAgent> logger,
        IAgentMemoryService? agentMemoryService = null)
        : base(chatClient, skillManager, logger, agentMemoryService) { }

    public override string Name => "AnalysisAgent";
    public override string Description => "Análise de dados, pesquisa, comparação e geração de insights.";
    public override AgentTier Tier => AgentTier.Specialist;
    public override string Domain => "analysis";
    public override IEnumerable<string> AvailableTools => new[] { "search", "database", "charts" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um analista de dados e pesquisador. Ajuda com:
        - Análise de dados e métricas
        - Pesquisa e comparação de alternativas
        - Geração de insights e recomendações
        - Sumarização de informações complexas
        Seja preciso, cite fontes quando possível e quantifique resultados.
        """;
}
