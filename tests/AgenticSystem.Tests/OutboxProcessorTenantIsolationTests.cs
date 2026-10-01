using System.Reflection;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgenticSystem.Tests;

public sealed class TenantScopedOutboxNotification(string TenantId) : INotification
{
    public string TenantId { get; } = TenantId;
}

public sealed class OutboxProcessorTenantIsolationTests
{
    [Fact]
    public async Task ProcessOutboxMessages_DispatchesAndUpdatesEachMessageUnderItsRealTenant()
    {
        var tenantA = $"outbox-a-{Guid.NewGuid():N}";
        var tenantB = $"outbox-b-{Guid.NewGuid():N}";
        var tenantAccessor = new TenantContextAccessor();
        var systemOperations = new SystemOperationContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"outbox-tenant-isolation-{Guid.NewGuid():N}")
            .Options;

        foreach (var tenantId in new[] { tenantA, tenantB })
        {
            using var tenantScope = tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });
            await using var seed = new AgenticDbContext(options, tenantAccessor);
            seed.OutboxMessages.Add(new OutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EventType = typeof(TenantScopedOutboxNotification).AssemblyQualifiedName!,
                PayloadJson = JsonSerializer.Serialize(new TenantScopedOutboxNotification(tenantId)),
                CreatedAt = DateTime.UtcNow.AddSeconds(tenantId == tenantA ? -2 : -1)
            });
            await seed.SaveChangesAsync();
        }

        var dispatchedUnder = new List<string>();
        var publisher = Substitute.For<IPublisher>();
        publisher.Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                dispatchedUnder.Add(tenantAccessor.CurrentTenantId);
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton<AgenticDbContext>(_ => new AgenticDbContext(options, tenantAccessor));
        services.AddSingleton(publisher);
        using var provider = services.BuildServiceProvider();
        var processor = new OutboxProcessorBackgroundService(
            provider,
            Substitute.For<ILogger<OutboxProcessorBackgroundService>>(),
            tenantAccessor,
            systemOperations);

        var method = typeof(OutboxProcessorBackgroundService).GetMethod(
            "ProcessOutboxMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(processor, [CancellationToken.None])!;

        dispatchedUnder.Should().Equal(tenantA, tenantB);
        await using (var checkA = new AgenticDbContext(options, tenantAccessor))
        {
            using var tenantScope = tenantAccessor.BeginScope(new TenantContext { TenantId = tenantA });
            var message = await checkA.OutboxMessages.SingleAsync();
            message.TenantId.Should().Be(tenantA);
            message.ProcessedAt.Should().NotBeNull();
            message.Error.Should().BeNull();
        }
        await using (var checkB = new AgenticDbContext(options, tenantAccessor))
        {
            using var tenantScope = tenantAccessor.BeginScope(new TenantContext { TenantId = tenantB });
            var message = await checkB.OutboxMessages.SingleAsync();
            message.TenantId.Should().Be(tenantB);
            message.ProcessedAt.Should().NotBeNull();
            message.Error.Should().BeNull();
        }
    }
}
