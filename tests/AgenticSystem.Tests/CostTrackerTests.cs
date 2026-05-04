using FluentAssertions;
using AgenticSystem.Infrastructure.Gateway;

namespace AgenticSystem.Tests;

public class CostTrackerTests
{
    [Fact]
    public void RecordCost_TracksCostCorrectly()
    {
        var tracker = new CostTracker(defaultDailyBudget: 100m);

        tracker.RecordCost("openai", "llm", 0.50m);
        tracker.RecordCost("openai", "llm", 0.30m);

        tracker.GetServiceCost("openai").Should().Be(0.80m);
    }

    [Fact]
    public void GetServiceCost_UnknownService_ReturnsZero()
    {
        var tracker = new CostTracker();
        tracker.GetServiceCost("unknown").Should().Be(0);
    }

    [Fact]
    public void SetBudget_UpdatesServiceBudget()
    {
        var tracker = new CostTracker();

        tracker.SetBudget("openai", 25.00m);
        tracker.RecordCost("openai", "llm", 20.00m);

        var report = tracker.GetReport();
        report.BudgetAlert.Should().BeTrue(); // 20/25 = 80%
    }

    [Fact]
    public void GetReport_AggregatesByServiceAndCategory()
    {
        var tracker = new CostTracker(defaultDailyBudget: 100m);

        tracker.RecordCost("openai", "llm", 1.00m);
        tracker.RecordCost("claude", "llm", 2.00m);
        tracker.RecordCost("search-api", "search", 0.50m);

        var report = tracker.GetReport();
        report.TotalCost.Should().Be(3.50m);
        report.CostByService.Should().ContainKey("openai").WhoseValue.Should().Be(1.00m);
        report.CostByService.Should().ContainKey("claude").WhoseValue.Should().Be(2.00m);
        report.CostByCategory.Should().ContainKey("llm").WhoseValue.Should().Be(3.00m);
        report.CostByCategory.Should().ContainKey("search").WhoseValue.Should().Be(0.50m);
    }

    [Fact]
    public void GetReport_BudgetAlert_WhenAt80Percent()
    {
        var tracker = new CostTracker(defaultDailyBudget: 10.00m);

        tracker.RecordCost("openai", "llm", 8.00m); // 80% of 10

        var report = tracker.GetReport();
        report.BudgetAlert.Should().BeTrue();
    }

    [Fact]
    public void GetReport_NoBudgetAlert_WhenBelow80Percent()
    {
        var tracker = new CostTracker(defaultDailyBudget: 10.00m);

        tracker.RecordCost("openai", "llm", 7.00m); // 70% of 10

        var report = tracker.GetReport();
        report.BudgetAlert.Should().BeFalse();
    }
}
