using FluentAssertions;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Gateway;

namespace AgenticSystem.Tests;

public class CircuitBreakerTests
{
    private CircuitBreakerConfig CreateConfig(int threshold = 3, int breakSeconds = 1)
        => new()
        {
            FailureThreshold = threshold,
            SamplingDuration = TimeSpan.FromMinutes(1),
            BreakDuration = TimeSpan.FromSeconds(breakSeconds)
        };

    [Fact]
    public void NewCircuitBreaker_StartsInClosedState()
    {
        var cb = new CircuitBreaker(CreateConfig());
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void AllowRequest_WhenClosed_ReturnsTrue()
    {
        var cb = new CircuitBreaker(CreateConfig());
        cb.AllowRequest().Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_BelowThreshold_StaysClosed()
    {
        var cb = new CircuitBreaker(CreateConfig(threshold: 3));

        cb.RecordFailure();
        cb.RecordFailure();

        cb.State.Should().Be(CircuitState.Closed);
        cb.AllowRequest().Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_AtThreshold_OpensCircuit()
    {
        var cb = new CircuitBreaker(CreateConfig(threshold: 3));

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();

        cb.State.Should().Be(CircuitState.Open);
        cb.AllowRequest().Should().BeFalse();
    }

    [Fact]
    public void RecordSuccess_InHalfOpen_ClosesCircuit()
    {
        var cb = new CircuitBreaker(CreateConfig(threshold: 1, breakSeconds: 0));

        cb.RecordFailure(); // Opens circuit internally

        // BreakDuration = 0s → immediately transitions to HalfOpen on first State access
        cb.State.Should().Be(CircuitState.HalfOpen);
        cb.AllowRequest().Should().BeTrue();

        cb.RecordSuccess();
        cb.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void Reset_ClearsFailuresAndCloses()
    {
        var cb = new CircuitBreaker(CreateConfig(threshold: 1));

        cb.RecordFailure();
        cb.State.Should().Be(CircuitState.Open);

        cb.Reset();
        cb.State.Should().Be(CircuitState.Closed);
        cb.AllowRequest().Should().BeTrue();
    }
}
