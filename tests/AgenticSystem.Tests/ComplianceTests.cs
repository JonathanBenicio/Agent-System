using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class ComplianceTests
{
    private readonly ISessionStore _sessionStore;
    private readonly IAuditLog _auditLog;
    private readonly IVectorStore _vectorStore;
    private readonly ComplianceService _compliance;

    public ComplianceTests()
    {
        _sessionStore = Substitute.For<ISessionStore>();
        _auditLog = Substitute.For<IAuditLog>();
        _vectorStore = Substitute.For<IVectorStore>();
        _compliance = new ComplianceService(
            _sessionStore,
            _auditLog,
            _vectorStore,
            Substitute.For<ILogger<ComplianceService>>());
    }

    [Fact]
    public async Task SetRetentionPolicyAsync_ShouldAddPolicy()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Name = "History Retention",
            Scope = RetentionScope.ConversationHistory,
            RetentionPeriod = TimeSpan.FromDays(30),
            ActionOnExpiry = RetentionAction.Delete
        };

        // Act
        await _compliance.SetRetentionPolicyAsync(policy);
        var retrieved = await _compliance.GetRetentionPolicyAsync();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("History Retention");
    }

    [Fact]
    public async Task SubmitDataRequestAsync_ShouldRecordRequest()
    {
        // Arrange
        var request = new DataSubjectRequest
        {
            RequestType = DataSubjectRequestType.Export,
            SubjectId = "user-1",
            SubjectEmail = "user@example.com"
        };

        // Act
        var result = await _compliance.SubmitDataRequestAsync(request);

        // Assert
        result.Status.Should().Be(DataSubjectRequestStatus.Pending);
        result.SubjectId.Should().Be("user-1");
    }
}
