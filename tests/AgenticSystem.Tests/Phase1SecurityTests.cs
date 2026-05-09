using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgenticSystem.Tests;

public class AgentExecutionStateMachineTests
{
    [Fact]
    public void InitialState_IsCreated()
    {
        var sm = new AgentExecutionStateMachine();
        Assert.Equal(AgentExecutionState.Created, sm.CurrentState);
        Assert.False(sm.IsTerminal);
    }

    [Fact]
    public void Transition_Created_To_Idle_Succeeds()
    {
        var sm = new AgentExecutionStateMachine();
        var t = sm.Transition(AgentExecutionState.Idle, "init");
        Assert.Equal(AgentExecutionState.Idle, sm.CurrentState);
        Assert.Equal(AgentExecutionState.Created, t.From);
        Assert.Equal(AgentExecutionState.Idle, t.To);
        Assert.Equal("init", t.Trigger);
    }

    [Fact]
    public void Transition_Invalid_Throws()
    {
        var sm = new AgentExecutionStateMachine();
        Assert.Throws<InvalidOperationException>(() =>
            sm.Transition(AgentExecutionState.Completed, "skip"));
    }

    [Fact]
    public void FullHappyPath_ReachesCompleted()
    {
        var sm = new AgentExecutionStateMachine();
        sm.Transition(AgentExecutionState.Idle, "init");
        sm.Transition(AgentExecutionState.Planning, "user-request");
        sm.Transition(AgentExecutionState.RetrievingContext, "rag-needed");
        sm.Transition(AgentExecutionState.CallingLLM, "context-ready");
        sm.Transition(AgentExecutionState.Reflecting, "response-received");
        sm.Transition(AgentExecutionState.Completed, "reflection-ok");

        Assert.Equal(AgentExecutionState.Completed, sm.CurrentState);
        Assert.True(sm.IsTerminal);
        Assert.Equal(6, sm.History.Count);
    }

    [Fact]
    public void ToolApprovalPath_WaitsAndResumes()
    {
        var sm = new AgentExecutionStateMachine();
        sm.Transition(AgentExecutionState.Idle, "init");
        sm.Transition(AgentExecutionState.Planning, "request");
        sm.Transition(AgentExecutionState.SelectingTool, "tool-needed");
        sm.Transition(AgentExecutionState.WaitingHumanApproval, "high-risk-tool");

        Assert.Equal(AgentExecutionState.WaitingHumanApproval, sm.CurrentState);
        Assert.False(sm.IsTerminal);

        sm.Transition(AgentExecutionState.ExecutingTool, "approved");
        Assert.Equal(AgentExecutionState.ExecutingTool, sm.CurrentState);
    }

    [Fact]
    public void CanTransition_ReturnsCorrectResults()
    {
        var sm = new AgentExecutionStateMachine();
        Assert.True(sm.CanTransition(AgentExecutionState.Idle));
        Assert.True(sm.CanTransition(AgentExecutionState.Cancelled));
        Assert.False(sm.CanTransition(AgentExecutionState.Completed));
        Assert.False(sm.CanTransition(AgentExecutionState.ExecutingTool));
    }

    [Fact]
    public void Completed_IsTerminal_NoTransitionsAllowed()
    {
        var sm = new AgentExecutionStateMachine();
        sm.Transition(AgentExecutionState.Idle, "init");
        sm.Transition(AgentExecutionState.CallingLLM, "direct");
        sm.Transition(AgentExecutionState.Completed, "done");

        Assert.True(sm.IsTerminal);
        Assert.Empty(sm.GetValidTransitions());
        Assert.Throws<InvalidOperationException>(() =>
            sm.Transition(AgentExecutionState.Idle, "restart"));
    }

    [Fact]
    public void Failed_CanTransitionBack_To_Idle()
    {
        var sm = new AgentExecutionStateMachine();
        sm.Transition(AgentExecutionState.Idle, "init");
        sm.Transition(AgentExecutionState.Planning, "request");
        sm.Transition(AgentExecutionState.Failed, "error");

        Assert.True(sm.IsTerminal);
        Assert.True(sm.CanTransition(AgentExecutionState.Idle));

        sm.Transition(AgentExecutionState.Idle, "retry");
        Assert.Equal(AgentExecutionState.Idle, sm.CurrentState);
        Assert.False(sm.IsTerminal);
    }

    [Fact]
    public void History_RecordsAllTransitions()
    {
        var sm = new AgentExecutionStateMachine { SessionId = "test-session" };
        sm.Transition(AgentExecutionState.Idle, "init", "Agent1");
        sm.Transition(AgentExecutionState.Planning, "request", "Agent1", "planning task X");

        var history = sm.History;
        Assert.Equal(2, history.Count);
        Assert.Equal("Agent1", history[0].AgentName);
        Assert.Equal("planning task X", history[1].Detail);
    }

    [Fact]
    public void ElapsedTime_ReflectsTransitionHistory()
    {
        var sm = new AgentExecutionStateMachine();
        sm.Transition(AgentExecutionState.Idle, "init");
        sm.Transition(AgentExecutionState.CallingLLM, "call");
        sm.Transition(AgentExecutionState.Completed, "done");

        Assert.True(sm.ElapsedTime >= TimeSpan.Zero);
    }
}

public class PolicyEngineTests
{
    private PolicyEngine CreateEngine() => new(NullLogger<PolicyEngine>.Instance);

    [Fact]
    public async Task NoPolicies_DefaultAllows()
    {
        var engine = CreateEngine();
        var result = await engine.EvaluateAsync(new PolicyContext { ToolName = "search" });
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task DeniedTool_ReturnsDeny()
    {
        var engine = CreateEngine();
        await engine.SavePolicyAsync(new AgentPolicy
        {
            Name = "block-email",
            DeniedTools = ["send-email"],
            Priority = 10
        });

        var result = await engine.EvaluateAsync(new PolicyContext { ToolName = "send-email" });
        Assert.False(result.Allowed);
        Assert.Single(result.Violations);
        Assert.Equal(PolicyViolationType.ToolDenied, result.Violations[0].Type);
    }

    [Fact]
    public async Task AllowedCategory_PassesCheck()
    {
        var engine = CreateEngine();
        await engine.SavePolicyAsync(new AgentPolicy
        {
            Name = "search-only",
            AllowedToolCategories = ["Search", "Database"],
            Priority = 10
        });

        var result = await engine.EvaluateAsync(new PolicyContext { ToolName = "google", ToolCategory = "Search" });
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task DisallowedCategory_ReturnsDeny()
    {
        var engine = CreateEngine();
        await engine.SavePolicyAsync(new AgentPolicy
        {
            Name = "no-email",
            AllowedToolCategories = ["Search"],
            Priority = 10
        });

        var result = await engine.EvaluateAsync(new PolicyContext { ToolName = "mailer", ToolCategory = "Email" });
        Assert.False(result.Allowed);
        Assert.Equal(PolicyViolationType.CategoryDenied, result.Violations[0].Type);
    }

    [Fact]
    public async Task BudgetExceeded_ReturnsDeny()
    {
        var engine = CreateEngine();
        await engine.SavePolicyAsync(new AgentPolicy
        {
            Name = "budget-limit",
            MaxCostPerRequest = 0.10m,
            Priority = 10
        });

        var result = await engine.EvaluateAsync(new PolicyContext { EstimatedCost = 0.50m });
        Assert.False(result.Allowed);
        Assert.Equal(PolicyViolationType.BudgetExceeded, result.Violations[0].Type);
    }

    [Fact]
    public async Task AutonomyExceeded_RequiresApproval()
    {
        var engine = CreateEngine();
        await engine.SavePolicyAsync(new AgentPolicy
        {
            Name = "supervised-mode",
            MaxAutonomyLevel = AutonomyLevel.Supervised,
            Priority = 10
        });

        var result = await engine.EvaluateAsync(new PolicyContext { RiskLevel = ToolRiskLevel.High });
        Assert.False(result.Allowed);
        Assert.True(result.RequiresApproval);
    }

    [Fact]
    public async Task AgentPatternFiltering_Works()
    {
        var engine = CreateEngine();
        await engine.SavePolicyAsync(new AgentPolicy
        {
            Name = "master-only",
            AgentNamePattern = "Master*",
            DeniedTools = ["dangerous-tool"],
            Priority = 10
        });

        // Agent matching pattern → denied
        var r1 = await engine.EvaluateAsync(new PolicyContext { AgentName = "MasterAgent", ToolName = "dangerous-tool" });
        Assert.False(r1.Allowed);

        // Agent not matching → no policy applies → allowed
        var r2 = await engine.EvaluateAsync(new PolicyContext { AgentName = "HelperAgent", ToolName = "dangerous-tool" });
        Assert.True(r2.Allowed);
    }

    [Fact]
    public async Task DeletePolicy_RemovesIt()
    {
        var engine = CreateEngine();
        var policy = new AgentPolicy { Name = "temp", DeniedTools = ["x"], Priority = 10 };
        await engine.SavePolicyAsync(policy);
        await engine.DeletePolicyAsync(policy.Id);

        var result = await engine.EvaluateAsync(new PolicyContext { ToolName = "x" });
        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task InactivePolicy_IsIgnored()
    {
        var engine = CreateEngine();
        await engine.SavePolicyAsync(new AgentPolicy
        {
            Name = "disabled",
            DeniedTools = ["x"],
            IsActive = false,
            Priority = 10
        });

        var result = await engine.EvaluateAsync(new PolicyContext { ToolName = "x" });
        Assert.True(result.Allowed);
    }
}

public class InMemoryPermissionServiceTests
{
    private InMemoryPermissionService CreateService() => new(NullLogger<InMemoryPermissionService>.Instance);

    [Fact]
    public async Task NoRoles_HasNoPermissions()
    {
        var svc = CreateService();
        var has = await svc.HasPermissionAsync("user1", "agents/main", Permission.Execute);
        Assert.False(has);
    }

    [Fact]
    public async Task AssignOwner_HasAllPermissions()
    {
        var svc = CreateService();
        await svc.AssignRoleAsync("user1", "Owner");
        var has = await svc.HasPermissionAsync("user1", "agents/main", Permission.Admin);
        Assert.True(has);
    }

    [Fact]
    public async Task AssignViewer_HasOnlyRead()
    {
        var svc = CreateService();
        await svc.AssignRoleAsync("user1", "Viewer");

        Assert.True(await svc.HasPermissionAsync("user1", "anything", Permission.Read));
        Assert.False(await svc.HasPermissionAsync("user1", "anything", Permission.Write));
        Assert.False(await svc.HasPermissionAsync("user1", "anything", Permission.Execute));
    }

    [Fact]
    public async Task RevokeRole_RemovesPermissions()
    {
        var svc = CreateService();
        await svc.AssignRoleAsync("user1", "Admin");
        Assert.True(await svc.HasPermissionAsync("user1", "x", Permission.ManageAgents));

        await svc.RevokeRoleAsync("user1", "Admin");
        Assert.False(await svc.HasPermissionAsync("user1", "x", Permission.ManageAgents));
    }

    [Fact]
    public async Task GetRoles_ReturnsActiveAssignments()
    {
        var svc = CreateService();
        await svc.AssignRoleAsync("user1", "Operator", "tenant-a");
        await svc.AssignRoleAsync("user1", "Viewer");

        var roles = await svc.GetRolesAsync("user1");
        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public async Task DuplicateAssignment_IsIgnored()
    {
        var svc = CreateService();
        await svc.AssignRoleAsync("user1", "Viewer");
        await svc.AssignRoleAsync("user1", "Viewer");

        var roles = await svc.GetRolesAsync("user1");
        Assert.Single(roles);
    }

    [Fact]
    public async Task InvalidRole_ThrowsArgumentException()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AssignRoleAsync("user1", "NonExistentRole"));
    }

    [Fact]
    public async Task GetEffectivePermissions_AggregatesMultipleRoles()
    {
        var svc = CreateService();
        await svc.AssignRoleAsync("user1", "Viewer");
        await svc.AssignRoleAsync("user1", "Operator");

        var perms = await svc.GetEffectivePermissionsAsync("user1", "docs/report");
        Assert.True((perms & Permission.Read) == Permission.Read);
        Assert.True((perms & Permission.Write) == Permission.Write);
        Assert.True((perms & Permission.Execute) == Permission.Execute);
    }
}

public class InMemoryAuditLogTests
{
    private InMemoryAuditLog CreateLog() => new(NullLogger<InMemoryAuditLog>.Instance);

    [Fact]
    public async Task Record_And_Query_Works()
    {
        var log = CreateLog();
        await log.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.ToolCall,
            Action = "search.execute",
            AgentName = "TestAgent"
        });

        var results = await log.QueryAsync(new AuditQuery { Category = AuditCategory.ToolCall });
        Assert.Single(results);
        Assert.Equal("search.execute", results[0].Action);
    }

    [Fact]
    public async Task Query_FiltersByCategory()
    {
        var log = CreateLog();
        await log.RecordAsync(new AuditEntry { Category = AuditCategory.ToolCall, Action = "tool-action" });
        await log.RecordAsync(new AuditEntry { Category = AuditCategory.ConfigChange, Action = "config-action" });

        var toolEntries = await log.QueryAsync(new AuditQuery { Category = AuditCategory.ToolCall });
        Assert.Single(toolEntries);
        Assert.Equal("tool-action", toolEntries[0].Action);
    }

    [Fact]
    public async Task Query_FiltersByDateRange()
    {
        var log = CreateLog();
        await log.RecordAsync(new AuditEntry { Category = AuditCategory.AgentExecution, Action = "exec" });

        var future = await log.QueryAsync(new AuditQuery { From = DateTime.UtcNow.AddHours(1) });
        Assert.Empty(future);

        var past = await log.QueryAsync(new AuditQuery { From = DateTime.UtcNow.AddHours(-1) });
        Assert.Single(past);
    }

    [Fact]
    public async Task Query_RespectsLimit()
    {
        var log = CreateLog();
        for (var i = 0; i < 10; i++)
            await log.RecordAsync(new AuditEntry { Category = AuditCategory.ToolCall, Action = $"action-{i}" });

        var results = await log.QueryAsync(new AuditQuery { Limit = 3 });
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task Count_ReturnsTotal()
    {
        var log = CreateLog();
        for (var i = 0; i < 5; i++)
            await log.RecordAsync(new AuditEntry { Category = AuditCategory.ToolCall, Action = $"a{i}" });

        var count = await log.CountAsync(new AuditQuery { Category = AuditCategory.ToolCall });
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task Query_FiltersBySuccess()
    {
        var log = CreateLog();
        await log.RecordAsync(new AuditEntry { Action = "ok", Success = true });
        await log.RecordAsync(new AuditEntry { Action = "fail", Success = false, ErrorMessage = "boom" });

        var successes = await log.QueryAsync(new AuditQuery { SuccessOnly = true });
        Assert.Single(successes);
        Assert.Equal("ok", successes[0].Action);
    }
}

public class StateMachineIntegrationTests
{
    [Fact]
    public void StateMachine_InitializedInCoordinator()
    {
        var sessionManager = Substitute.For<ISessionManager>();
        var coordinator = new AgentRuntimeCoordinator(
            sessionManager,
            NullLogger<AgentRuntimeCoordinator>.Instance);

        using var scope = coordinator.BeginExecutionScope("session-1", new UserContext { UserId = "user1" });

        Assert.NotNull(coordinator.CurrentStateMachine);
        Assert.Equal(AgentExecutionState.Idle, coordinator.CurrentStateMachine!.CurrentState);
        Assert.Equal("session-1", coordinator.CurrentStateMachine.SessionId);
    }

    [Fact]
    public void StateMachine_TracksThroughFullLifecycle()
    {
        var sessionManager = Substitute.For<ISessionManager>();
        var coordinator = new AgentRuntimeCoordinator(
            sessionManager,
            NullLogger<AgentRuntimeCoordinator>.Instance);

        using var scope = coordinator.BeginExecutionScope("session-2", new UserContext { UserId = "u2" });
        var sm = coordinator.CurrentStateMachine!;

        sm.Transition(AgentExecutionState.Planning, "user-request");
        sm.Transition(AgentExecutionState.CallingLLM, "direct-call");
        sm.Transition(AgentExecutionState.Completed, "done");

        Assert.True(sm.IsTerminal);
        Assert.Equal(4, sm.History.Count); // Idle(auto) + Planning + CallingLLM + Completed
    }
}

public class PolicyEngineGovernanceIntegrationTests
{
    [Fact]
    public async Task GovernanceService_DeniesWhenPolicyDenies()
    {
        var coordinator = Substitute.For<IAgentRuntimeCoordinator>();
        coordinator.CurrentAllowedTools.Returns(Array.Empty<string>());
        coordinator.CurrentAgentName.Returns("TestAgent");

        var policyEngine = new PolicyEngine(NullLogger<PolicyEngine>.Instance);
        await policyEngine.SavePolicyAsync(new AgentPolicy
        {
            Name = "block-email-tools",
            DeniedTools = ["email-sender"],
            Priority = 10
        });

        var governance = new ToolGovernanceService(coordinator, policyEngine, NullLogger<ToolGovernanceService>.Instance);

        var tool = Substitute.For<ITool>();
        tool.Id.Returns("email-sender");
        tool.Name.Returns("email-sender");
        tool.Category.Returns(ToolCategory.Email);

        var decision = await governance.EvaluateAsync(tool, new ToolInput { Action = "send" });

        Assert.False(decision.Allowed);
        Assert.Contains("denied", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GovernanceService_AllowsWhenNoPolicyBlocks()
    {
        var coordinator = Substitute.For<IAgentRuntimeCoordinator>();
        coordinator.CurrentAllowedTools.Returns(Array.Empty<string>());

        var policyEngine = new PolicyEngine(NullLogger<PolicyEngine>.Instance);

        var governance = new ToolGovernanceService(coordinator, policyEngine, NullLogger<ToolGovernanceService>.Instance);

        var tool = Substitute.For<ITool>();
        tool.Id.Returns("search-tool");
        tool.Name.Returns("search-tool");
        tool.Category.Returns(ToolCategory.Search);

        var decision = await governance.EvaluateAsync(tool, new ToolInput { Action = "search" });

        Assert.True(decision.Allowed);
    }
}

public class SecretsVaultTests
{
    private static ConfigManager CreateManager()
    {
        var store = new InMemoryConfigStore();
        var encryption = new AesConfigEncryptionService(null);
        var notifier = new ConfigReloadNotifier();
        return new ConfigManager(store, encryption, notifier, NullLogger<ConfigManager>.Instance);
    }

    [Fact]
    public async Task RotateSecret_UpdatesEncryptedValue()
    {
        var mgr = CreateManager();

        await mgr.SetAsync(new ConfigEntryRequest
        {
            Key = "api-key",
            Value = "old-secret",
            IsSecret = true,
            Category = ConfigCategory.Credentials
        });

        var rotated = await mgr.RotateSecretAsync("api-key", "new-secret");

        Assert.Equal("********", rotated.Value);
        Assert.True(rotated.IsSecret);

        // Verify the internal resolved value changed
        var resolved = await mgr.ResolveValueAsync("api-key");
        Assert.Equal("new-secret", resolved);
    }

    [Fact]
    public async Task RotateSecret_NonSecret_Throws()
    {
        var mgr = CreateManager();

        await mgr.SetAsync(new ConfigEntryRequest
        {
            Key = "app-name",
            Value = "MyApp",
            IsSecret = false,
            Category = ConfigCategory.General
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mgr.RotateSecretAsync("app-name", "new-value"));
    }

    [Fact]
    public async Task RotateSecret_LogsRotation()
    {
        var mgr = CreateManager();

        await mgr.SetAsync(new ConfigEntryRequest
        {
            Key = "db-password",
            Value = "pass1",
            IsSecret = true,
            Category = ConfigCategory.Credentials
        });

        await mgr.RotateSecretAsync("db-password", "pass2");

        var logs = (await mgr.GetAuditLogAsync("db-password")).ToList();
        Assert.Contains(logs, l => l.Action == "Rotated");
    }

    [Fact]
    public async Task GetExpiredSecrets_ReturnsExpired()
    {
        var mgr = CreateManager();

        await mgr.SetAsync(new ConfigEntryRequest
        {
            Key = "expired-key",
            Value = "secret",
            IsSecret = true,
            Category = ConfigCategory.Credentials,
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Already expired
        });

        await mgr.SetAsync(new ConfigEntryRequest
        {
            Key = "valid-key",
            Value = "secret2",
            IsSecret = true,
            Category = ConfigCategory.Credentials,
            ExpiresAt = DateTime.UtcNow.AddDays(30) // Still valid
        });

        var expired = (await mgr.GetExpiredSecretsAsync(TimeSpan.Zero)).ToList();
        Assert.Single(expired);
        Assert.Equal("expired-key", expired[0].Key);
        Assert.Equal("********", expired[0].Value);
    }

    [Fact]
    public async Task GetExpiredSecrets_WithLookahead_IncludesSoonToExpire()
    {
        var mgr = CreateManager();

        await mgr.SetAsync(new ConfigEntryRequest
        {
            Key = "expiring-soon",
            Value = "secret",
            IsSecret = true,
            Category = ConfigCategory.Credentials,
            ExpiresAt = DateTime.UtcNow.AddDays(3)
        });

        var withWindow = (await mgr.GetExpiredSecretsAsync(TimeSpan.FromDays(7))).ToList();
        Assert.Single(withWindow);

        var withoutWindow = (await mgr.GetExpiredSecretsAsync(TimeSpan.FromDays(1))).ToList();
        Assert.Empty(withoutWindow);
    }
}
