using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Gateway;

namespace AgenticSystem.Tests.MultiTenancy;

public class TenantCostTrackerTests
{
    private readonly CostTracker _tracker = new();

    [Fact]
    public void RecordCost_WithoutTenantId_WorksAsGlobal()
    {
        _tracker.RecordCost("openai", "llm", 0.05m);

        var report = _tracker.GetReport();
        report.CostByService.Should().ContainKey("openai");
    }

    [Fact]
    public void RecordCost_WithTenantId_IsolatesByTenant()
    {
        _tracker.RecordCost("openai", "llm", 0.10m, tenantId: "tenant-a");
        _tracker.RecordCost("openai", "llm", 0.20m, tenantId: "tenant-b");

        var reportA = _tracker.GetReport(tenantId: "tenant-a");
        var reportB = _tracker.GetReport(tenantId: "tenant-b");

        reportA.CostByService.Should().ContainKey("openai");
        reportB.CostByService.Should().ContainKey("openai");
        reportA.TotalCost.Should().Be(0.10m);
        reportB.TotalCost.Should().Be(0.20m);
    }

    [Fact]
    public void GetReport_TenantFiltered_DoesNotIncludeOtherTenants()
    {
        _tracker.RecordCost("openai", "llm", 0.10m, tenantId: "t1");
        _tracker.RecordCost("openai", "llm", 0.20m, tenantId: "t2");
        _tracker.RecordCost("openai", "llm", 0.30m); // global

        var report = _tracker.GetReport(tenantId: "t1");
        report.TotalCost.Should().Be(0.10m);
        report.CostByService.Should().HaveCount(1);
    }

    [Fact]
    public void SetBudget_WithTenantId_IsIsolated()
    {
        _tracker.SetBudget("openai", 100m, tenantId: "tenant-a");
        _tracker.SetBudget("openai", 200m, tenantId: "tenant-b");

        _tracker.RecordCost("openai", "llm", 50m, tenantId: "tenant-a");
        _tracker.RecordCost("openai", "llm", 50m, tenantId: "tenant-b");

        var reportA = _tracker.GetReport(tenantId: "tenant-a");
        var reportB = _tracker.GetReport(tenantId: "tenant-b");

        reportA.DailyBudget.Should().Be(100m);
        reportB.DailyBudget.Should().Be(200m);
    }

    [Fact]
    public void BackwardCompatibility_ExistingCallsWithoutTenant_StillWork()
    {
        _tracker.RecordCost("claude", "llm", 0.15m);
        _tracker.SetBudget("claude", 50m);

        var cost = _tracker.GetServiceCost("claude");
        cost.Should().Be(0.15m);

        var report = _tracker.GetReport();
        report.CostByService.Should().ContainKey("claude");
        report.DailyBudget.Should().Be(50m);
    }
}
