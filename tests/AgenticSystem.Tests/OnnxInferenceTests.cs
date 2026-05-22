using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services.Ml;
using AgenticSystem.Infrastructure.BackgroundServices;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Xunit;

namespace AgenticSystem.Tests;

public class OnnxInferenceTests
{
    [Fact]
    public async Task Queue_ShouldEnqueueAndDequeueSuccessfully()
    {
        // Arrange
        var queue = new OnnxInferenceQueue();
        var request = new OnnxInferenceJobRequest("job-1", "tenant-1", "model-1", "input.png");

        // Act
        await queue.EnqueueJobAsync(request, CancellationToken.None);
        var dequeued = await queue.DequeueJobAsync(CancellationToken.None);

        // Assert
        dequeued.Should().NotBeNull();
        dequeued.JobId.Should().Be("job-1");
        dequeued.TenantId.Should().Be("tenant-1");
        dequeued.ModelId.Should().Be("model-1");
        dequeued.InputImagePath.Should().Be("input.png");
    }

    [Fact]
    public async Task Worker_ReenqueueInterruptedJobsAsync_ShouldReenqueuePendingAndProcessingJobs()
    {
        // Arrange
        var dbName = $"onnx-worker-tests-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.Current.Returns(new TenantContext { TenantId = "tenant-1" });

        using var dbContext = new AgenticDbContext(options, tenantAccessor);
        dbContext.Database.EnsureCreated();

        // Seed with interrupted/completed jobs
        var job1 = new CustomOnnxInferenceJobEntity
        {
            Id = "job-1",
            TenantId = "tenant-1",
            ModelId = "model-1",
            Status = "Pending",
            InputImagePath = "/onnx-uploads/tenant-1/job-1_input.png",
            CreatedAt = DateTime.UtcNow
        };
        var job2 = new CustomOnnxInferenceJobEntity
        {
            Id = "job-2",
            TenantId = "tenant-1",
            ModelId = "model-1",
            Status = "Processing",
            InputImagePath = "/onnx-uploads/tenant-1/job-2_input.png",
            CreatedAt = DateTime.UtcNow
        };
        var job3 = new CustomOnnxInferenceJobEntity
        {
            Id = "job-3",
            TenantId = "tenant-1",
            ModelId = "model-1",
            Status = "Completed",
            InputImagePath = "/onnx-uploads/tenant-1/job-3_input.png",
            OutputImagePath = "/onnx-results/tenant-1/job-3_output.png",
            CreatedAt = DateTime.UtcNow
        };

        dbContext.CustomOnnxInferenceJobs.AddRange(job1, job2, job3);
        await dbContext.SaveChangesAsync();

        var serviceProvider = Substitute.For<IServiceProvider>();
        var serviceScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();

        serviceScope.ServiceProvider.Returns(serviceProvider);
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceProvider.GetService(typeof(AgenticDbContext)).Returns(dbContext);

        var queue = Substitute.For<IOnnxInferenceQueue>();
        var broadcaster = Substitute.For<IOnnxEventBroadcaster>();
        var logger = Substitute.For<ILogger<OnnxInferenceBackgroundWorker>>();

        var worker = new OnnxInferenceBackgroundWorker(serviceProvider, queue, broadcaster, logger);

        // Act
        // Cancel token in StartAsync immediately to prevent worker loop blocking, running only the recovery stage
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await worker.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation from Token during StartAsync
        }

        // Assert
        // Verify queue.EnqueueJobAsync was called for job1 and job2
        await queue.Received(1).EnqueueJobAsync(Arg.Is<OnnxInferenceJobRequest>(r => r.JobId == "job-1"), Arg.Any<CancellationToken>());
        await queue.Received(1).EnqueueJobAsync(Arg.Is<OnnxInferenceJobRequest>(r => r.JobId == "job-2"), Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueJobAsync(Arg.Is<OnnxInferenceJobRequest>(r => r.JobId == "job-3"), Arg.Any<CancellationToken>());

        // Verify that job2 was updated to "Pending" status in the DB
        var updatedJob2 = dbContext.CustomOnnxInferenceJobs.IgnoreQueryFilters().First(j => j.Id == "job-2");
        updatedJob2.Status.Should().Be("Pending");
    }
}
