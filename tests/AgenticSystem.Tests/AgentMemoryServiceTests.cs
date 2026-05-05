using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgenticSystem.Tests;

public class AgentMemoryServiceTests
{
    private readonly InMemoryAgentMemoryStore _store;
    private readonly AgentMemoryService _sut;

    public AgentMemoryServiceTests()
    {
        _store = new InMemoryAgentMemoryStore();
        _sut = new AgentMemoryService(_store, Substitute.For<ILogger<AgentMemoryService>>());
    }

    [Fact]
    public async Task GetRelevantMemoriesAsync_WithMatchingTerms_ReturnsMostRelevantEntries()
    {
        await _store.SaveAsync(new AgentMemoryEntry
        {
            UserId = "user-1",
            AgentName = "WorkAgent",
            MemoryType = AgentMemoryType.LearnedRule,
            Content = "Priorize respostas com Jira e incident response",
            Keywords = ["jira", "incident", "response"],
            Confidence = 0.9
        });
        await _store.SaveAsync(new AgentMemoryEntry
        {
            UserId = "user-1",
            AgentName = "WorkAgent",
            MemoryType = AgentMemoryType.Fact,
            Content = "Usuário gosta de resumos técnicos",
            Keywords = ["resumos", "tecnicos"],
            Confidence = 0.6
        });

        var result = await _sut.GetRelevantMemoriesAsync("WorkAgent", "user-1", "preciso revisar incidentes no jira", 1);

        result.Should().ContainSingle();
        result[0].Content.Should().Contain("Jira");
        result[0].UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordInteractionAsync_WithReflectionSuggestion_PersistsInteractionAndLearnedRules()
    {
        var context = new UserContext
        {
            UserId = "user-1",
            Role = "dev",
            Language = "pt-BR",
            Domains = ["work"]
        };

        var response = AgentResponse.Ok("Resposta com recomendação técnica", "WorkAgent", AgentTier.Master);
        response.Confidence = new ConfidenceScore { Value = 0.88, Level = ConfidenceLevel.High };

        var reflection = new Reflection
        {
            AgentName = "WorkAgent",
            SessionId = "session-1",
            LessonsLearned = ["Quando houver incidentes, priorize impacto e rollback"],
            ImprovementSuggestion = "Sempre confirmar criticidade antes de propor rollout",
            Severity = ReflectionSeverity.Warning
        };

        await _sut.RecordInteractionAsync("session-1", "WorkAgent", context, "como tratar incidentes", response, reflection);

        var stored = await _store.GetByAgentAsync("user-1", "WorkAgent");

        stored.Should().HaveCount(3);
        stored.Should().Contain(entry => entry.MemoryType == AgentMemoryType.Fact);
        stored.Should().Contain(entry => entry.MemoryType == AgentMemoryType.LearnedRule);
        stored.Should().Contain(entry => entry.MemoryType == AgentMemoryType.Correction);
    }
}