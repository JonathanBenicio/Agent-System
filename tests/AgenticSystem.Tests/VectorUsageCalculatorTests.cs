using AgenticSystem.Core.Models;
using Xunit;

namespace AgenticSystem.Tests;

public class VectorUsageCalculatorTests
{
    [Fact]
    public void Calculate_CountsLogicalDocumentsAndIngestedBytesInsteadOfChunks()
    {
        var stats = VectorUsageCalculator.Calculate("tenant-1", new[]
        {
            new VectorDocumentUsage("document-1-0", "document-1", 1024, 40, 16),
            new VectorDocumentUsage("document-1-1", "document-1", 0, 40, 16),
            new VectorDocumentUsage("legacy-chunk", null, null, 20, 8)
        });

        Assert.Equal(2, stats.DocumentCount);
        Assert.Equal(1052, stats.TotalBytes);
    }

    [Fact]
    public void TenantResourceLimits_AreAProjectionOfTenantLimits()
    {
        var planLimits = TenantLimits.FreeTier();
        var projection = TenantResourceLimits.From(planLimits);

        Assert.Equal(planLimits.MaxConcurrentSessions, projection.MaxConcurrentSessions);
        Assert.Equal(planLimits.MaxAgents, projection.MaxAgents);
        Assert.Equal(planLimits.MaxDocumentsMb, projection.MaxStorageMb);
        Assert.Equal(planLimits.MaxDocuments, projection.MaxDocuments);
        Assert.Equal((double)(planLimits.MaxDailyCostUsd * 30m), projection.MaxMonthlyBudgetUsd);
    }
}
