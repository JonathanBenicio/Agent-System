using System.Security.Claims;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgenticSystem.Tests;

public class WorkflowControllerTests
{
    private const string TenantId = "tenant-a";
    private const string UserId = "reviewer-1";
    private readonly IWorkflowStore _store = Substitute.For<IWorkflowStore>();
    private readonly IWorkflowEngine _engine = Substitute.For<IWorkflowEngine>();
    private readonly ITenantContextAccessor _tenant = Substitute.For<ITenantContextAccessor>();
    private readonly WorkflowController _controller;

    public WorkflowControllerTests()
    {
        _tenant.CurrentTenantId.Returns(TenantId);
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(ClaimTypes.Role, "Operator")
        ], "test");
        _controller = new WorkflowController(
            _store,
            _engine,
            Substitute.For<ILogger<WorkflowController>>(),
            _tenant)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };
    }

    [Fact]
    public async Task StartWorkflow_ReturnsBothExecutionIdNamesForExistingClients()
    {
        var definition = new WorkflowDefinition { Id = "wf-1", Name = "Dynamic" };
        var execution = new WorkflowExecution
        {
            Id = "exec-1",
            WorkflowId = definition.Id,
            WorkflowName = definition.Name,
            TenantId = TenantId,
            Status = WorkflowExecutionStatus.Running
        };
        _store.GetDefinitionAsync(TenantId, definition.Id, Arg.Any<CancellationToken>()).Returns(definition);
        _engine.StartAsync(TenantId, definition, Arg.Any<Dictionary<string, object>?>(), UserId, Arg.Any<CancellationToken>())
            .Returns(execution);

        var result = await _controller.StartWorkflow(definition.Id, new Dictionary<string, object>());

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        var value = accepted.Value!;
        value.GetType().GetProperty("id")!.GetValue(value).Should().Be(execution.Id);
        value.GetType().GetProperty("executionId")!.GetValue(value).Should().Be(execution.Id);
        _controller.Response.Headers.Location.ToString().Should().Contain(execution.Id);
    }

    [Fact]
    public async Task ApproveExecution_UsesAuthenticatedTenantAndReviewer()
    {
        var execution = new WorkflowExecution { Id = "exec-1", TenantId = TenantId };
        _engine.ApproveAsync(TenantId, execution.Id, UserId, Arg.Any<CancellationToken>()).Returns(execution);

        var result = await _controller.ApproveExecution(execution.Id);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(execution);
        await _engine.Received(1).ApproveAsync(TenantId, execution.Id, UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectExecution_PassesReasonAndAuthenticatedReviewer()
    {
        var execution = new WorkflowExecution { Id = "exec-2", TenantId = TenantId, Status = WorkflowExecutionStatus.Cancelled };
        _engine.RejectAsync(TenantId, execution.Id, UserId, "Needs revision", Arg.Any<CancellationToken>()).Returns(execution);

        var result = await _controller.RejectExecution(execution.Id, "Needs revision");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(execution);
        await _engine.Received(1).RejectAsync(TenantId, execution.Id, UserId, "Needs revision", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveExecution_RejectsViewerRole()
    {
        var viewer = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(ClaimTypes.Role, "Viewer")
        ], "test");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(viewer);

        var result = await _controller.ApproveExecution("exec-1");

        result.Should().BeOfType<ForbidResult>();
        await _engine.DidNotReceive().ApproveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
