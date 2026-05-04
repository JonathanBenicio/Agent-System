using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Agents;

public class PersonalAgent : BaseAgent
{
    public PersonalAgent(ILLMManager llmManager, ISkillManager skillManager, ILogger<PersonalAgent> logger)
        : base(llmManager, skillManager, logger) { }

    public override string Name => "PersonalAgent";
    public override string Description => "Gerencia tarefas pessoais, rotina, produtividade e organização.";
    public override AgentTier Tier => AgentTier.Master;
    public override string Domain => "personal";
    public override IEnumerable<string> AvailableTools => new[] { "calendar", "tasks", "notes", "reminders" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um assistente pessoal inteligente. Ajuda com:
        - Organização de tarefas e rotina diária
        - Gerenciamento de agenda e compromissos
        - Produtividade e hábitos
        - Notas e lembretes pessoais
        Seja direto, prático e proativo nas sugestões.
        """;
}

public class WorkAgent : BaseAgent
{
    public WorkAgent(ILLMManager llmManager, ISkillManager skillManager, ILogger<WorkAgent> logger)
        : base(llmManager, skillManager, logger) { }

    public override string Name => "WorkAgent";
    public override string Description => "Gerencia projetos, sprints, code review, documentação técnica e decisões de trabalho.";
    public override AgentTier Tier => AgentTier.Master;
    public override string Domain => "work";
    public override IEnumerable<string> AvailableTools => new[] { "jira", "github", "confluence", "email" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um assistente de trabalho focado em engenharia de software. Ajuda com:
        - Gerenciamento de projetos e sprints (Jira)
        - Code reviews e decisões técnicas
        - Documentação e ADRs
        - Comunicação profissional
        Use terminologia técnica apropriada. Seja objetivo e orientado a resultados.
        """;
}

public class LearningAgent : BaseAgent
{
    public LearningAgent(ILLMManager llmManager, ISkillManager skillManager, ILogger<LearningAgent> logger)
        : base(llmManager, skillManager, logger) { }

    public override string Name => "LearningAgent";
    public override string Description => "Gerencia aprendizado, cursos, resumos de conteúdo e planos de estudo.";
    public override AgentTier Tier => AgentTier.Master;
    public override string Domain => "learning";
    public override IEnumerable<string> AvailableTools => new[] { "notes", "search", "flashcards" };

    protected override string GetBaseSystemPrompt() =>
        """
        Você é um tutor inteligente especializado em aprendizado eficiente. Ajuda com:
        - Planos de estudo personalizados
        - Resumos de conteúdo técnico
        - Explicações de conceitos complexos
        - Flashcards e revisão espaçada
        Adapte a complexidade ao nível do usuário. Use analogias quando útil.
        """;
}
