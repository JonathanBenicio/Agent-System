using System.Security.Claims;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class SessionControllerTests
{
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<SessionController> _logger;
    private readonly SessionController _sut;
    private const string UserId = "user-123";

    public SessionControllerTests()
    {
        _sessionStore = Substitute.For<ISessionStore>();
        _logger = Substitute.For<ILogger<SessionController>>();
        _sut = new SessionController(_sessionStore, _logger);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, UserId)
        }, "TestAuth"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetSessions_ReturnsOk_WithMappedItems()
    {
        // Arrange
        var sessions = new List<SessionData>
        {
            new() { Id = "session-001", UserId = UserId, StartedAt = DateTime.UtcNow.AddMinutes(-10) },
            new() { Id = "session-002", UserId = UserId, StartedAt = DateTime.UtcNow.AddMinutes(-5) }
        };
        _sessionStore.GetByUserAsync(UserId, Arg.Any<int>()).Returns(sessions);

        // Act
        var result = await _sut.GetSessions();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = okResult.Value.Should().BeAssignableTo<IEnumerable<SessionListItemDto>>().Subject;
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSession_WhenFoundAndAuthorized_ReturnsOk()
    {
        // Arrange
        var session = new SessionData { Id = "session-001", UserId = UserId };
        _sessionStore.GetAsync("session-001").Returns(session);

        // Act
        var result = await _sut.GetSession("session-001");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSession_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _sessionStore.GetAsync("session-001").Returns((SessionData)null!);

        // Act
        var result = await _sut.GetSession("session-001");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSession_WhenOwnedByOtherUser_ReturnsNotFound()
    {
        // Arrange
        var session = new SessionData { Id = "session-001", UserId = "other-user" };
        _sessionStore.GetAsync("session-001").Returns(session);

        // Act
        var result = await _sut.GetSession("session-001");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteSession_ReturnsNoContent()
    {
        // Arrange
        var session = new SessionData { Id = "session-001", UserId = UserId };
        _sessionStore.GetAsync("session-001").Returns(session);

        // Act
        var result = await _sut.DeleteSession("session-001");

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _sessionStore.Received(1).DeleteAsync("session-001");
    }

    [Fact]
    public async Task UpdateTitle_UpdatesRuntimeSettings()
    {
        // Arrange
        var session = new SessionData { Id = "session-001", UserId = UserId };
        _sessionStore.GetAsync("session-001").Returns(session);
        var request = new UpdateSessionTitleRequest("New Title");

        // Act
        var result = await _sut.UpdateSessionTitle("session-001", request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        session.RuntimeSettings["title"].Should().Be("New Title");
        await _sessionStore.Received(1).SaveAsync(session);
    }
}
