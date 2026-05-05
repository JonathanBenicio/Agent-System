using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.RAG;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class LlmReRankerTests
{
    [Fact]
    public async Task ReRankAsync_WhenOnlyDedicatedProviderIsEnabled_UsesDedicatedScores()
    {
        var heuristicReRanker = new HeuristicReRanker(Substitute.For<ILogger<HeuristicReRanker>>());
        var chatClient = Substitute.For<IChatClient>();
        var dedicatedProvider = new TestDedicatedReRankerProvider(new Dictionary<string, double>
        {
            ["2"] = 0.98,
            ["1"] = 0.12
        });
        var settingsAccessor = Substitute.For<IRerankingSettingsAccessor>();
        settingsAccessor.GetCurrentOptionsAsync(Arg.Any<CancellationToken>())
            .Returns(new ReRankingOptions
            {
                Enabled = true,
                UseDedicatedProvider = true,
                DedicatedProvider = dedicatedProvider.Name,
                UseEmbeddingReRanking = false,
                UseLlmReRanking = false,
                CandidatePoolSize = 2,
                MinCandidateCountForLlm = 2,
                HeuristicConfidenceThreshold = 1.0,
                HeuristicConfidenceGap = 1.0,
                NeuralScoreWeight = 1.0
            });
        var sut = new LlmReRanker(
            heuristicReRanker,
            chatClient,
            [dedicatedProvider],
            embeddingGenerator: null,
            settingsAccessor,
            Substitute.For<ILogger<LlmReRanker>>());

        var candidates = new List<SearchMatch>
        {
            new()
            {
                Id = "1",
                Content = "Primeiro trecho sobre parcelamento e regras gerais.",
                Score = 0.60,
                Metadata = new Dictionary<string, string>()
            },
            new()
            {
                Id = "2",
                Content = "Segundo trecho específico sobre parcelamento sem juros.",
                Score = 0.59,
                Metadata = new Dictionary<string, string>()
            }
        };

        var result = await sut.ReRankAsync("parcelamento sem juros", candidates, topK: 2);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("2");
        result[0].Metadata["neural_rerank_provider"].Should().Be(dedicatedProvider.Name);
        await chatClient.DidNotReceiveWithAnyArgs().GetResponseAsync(default!, default!, default);
    }

    private sealed class TestDedicatedReRankerProvider : IDedicatedReRankerProvider
    {
        private readonly IReadOnlyDictionary<string, double> _scores;

        public TestDedicatedReRankerProvider(IReadOnlyDictionary<string, double> scores)
        {
            _scores = scores;
        }

        public string Name => "TestDedicatedProvider";

        public Task<DedicatedReRankingResult> ScoreAsync(
            string query,
            IReadOnlyList<RankedChunk> candidates,
            CancellationToken ct = default)
        {
            return Task.FromResult(new DedicatedReRankingResult(_scores, Name));
        }
    }
}