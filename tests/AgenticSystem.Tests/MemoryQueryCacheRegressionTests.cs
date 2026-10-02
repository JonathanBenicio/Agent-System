using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgenticSystem.Tests;

public sealed class MemoryQueryCacheRegressionTests
{
    [Fact]
    public async Task DifferentQueryAndLimitNeverReusePreviousContext_AndVectorizationInvalidates()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var vectors = Substitute.For<IVectorStore>();
        var version = 1;
        var requests = 0;
        vectors.SearchWithFiltersAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(call =>
            {
                requests++;
                return new SearchResult { Matches = [
                    new SearchMatch { Content = $"{call.ArgAt<string>(0)}-{version}-first",
                        Metadata = new() { ["memoryType"] = "fact" } },
                    new SearchMatch { Content = $"{call.ArgAt<string>(0)}-{version}-second",
                        Metadata = new() { ["memoryType"] = "fact" } }
                ] };
            });
        var service = new MemoryInjectionService(vectors, cache, NullLogger<MemoryInjectionService>.Instance);
        var first = await service.BuildMemoryContextAsync("alpha", "u", "a", 1);
        first.Should().Contain("alpha-1-first").And.NotContain("second");
        (await service.BuildMemoryContextAsync("alpha", "u", "a", 1)).Should().Be(first);
        requests.Should().Be(1);
        (await service.BuildMemoryContextAsync("beta", "u", "a", 1)).Should().Contain("beta").And.NotContain("alpha");
        (await service.BuildMemoryContextAsync("alpha", "u", "a", 2)).Should().Contain("second");
        await service.BuildMemoryContextAsync("alpha", "another-user", "a", 1);
        await service.BuildMemoryContextAsync("alpha", "u", "another-tenant", 1);
        requests.Should().Be(5);
        version = 2;
        await service.VectorizeInsightsAsync(new SessionInsights { Facts = ["new fact"] }, "u", "a", "s");
        (await service.BuildMemoryContextAsync("alpha", "u", "a", 1)).Should().Contain("alpha-2");
        requests.Should().Be(6);
    }

    [Fact]
    public async Task EmptyContextIsCachedOnlyForItsOwnQuery()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var vectors = Substitute.For<IVectorStore>();
        vectors.SearchWithFiltersAsync("empty", Arg.Any<Dictionary<string, string>>())
            .Returns(new SearchResult());
        vectors.SearchWithFiltersAsync("relevant", Arg.Any<Dictionary<string, string>>())
            .Returns(new SearchResult { Matches = [
                new SearchMatch { Content = "relevant fact", Metadata = new() { ["memoryType"] = "fact" } }
            ] });
        var service = new MemoryInjectionService(vectors, cache, NullLogger<MemoryInjectionService>.Instance);
        (await service.BuildMemoryContextAsync("empty", "u", "a")).Should().BeEmpty();
        (await service.BuildMemoryContextAsync("empty", "u", "a")).Should().BeEmpty();
        (await service.BuildMemoryContextAsync("relevant", "u", "a")).Should().Contain("relevant fact");
        await vectors.Received(1).SearchWithFiltersAsync("empty", Arg.Any<Dictionary<string, string>>());
    }
}
