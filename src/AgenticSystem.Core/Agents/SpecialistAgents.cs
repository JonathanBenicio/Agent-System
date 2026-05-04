using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Agents;

public class CalendarAgent : BaseAgent
{
    public CalendarAgent(ILLMManager llmManager, ISkillManager skillManager, ILogger<CalendarAgent> logger)
        : base(llmManager, skillManager, logger) { }

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
    public CreativeAgent(ILLMManager llmManager, ISkillManager skillManager, ILogger<CreativeAgent> logger)
        : base(llmManager, skillManager, logger) { }

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
    public AnalysisAgent(ILLMManager llmManager, ISkillManager skillManager, ILogger<AnalysisAgent> logger)
        : base(llmManager, skillManager, logger) { }

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
