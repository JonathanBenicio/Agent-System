using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>Claims and processes persisted workflow executions across application instances.</summary>
public sealed class WorkflowExecutionBackgroundService : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowExecutionBackgroundService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public WorkflowExecutionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkflowExecutionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow execution worker {WorkerId} started.", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessNextAsync(stoppingToken))
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow execution worker failed while claiming an execution.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _logger.LogInformation("Workflow execution worker {WorkerId} stopped.", _workerId);
    }

    internal async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
        var systemOperations = scope.ServiceProvider.GetRequiredService<ISystemOperationContextAccessor>();
        WorkflowExecutionClaim? claim;
        using (systemOperations.BeginScope(SystemOperationKind.ClaimWorkflowExecutions))
            claim = await store.ClaimNextExecutionAsync(_workerId, LeaseDuration, stoppingToken);
        if (claim is null)
            return false;

        var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        using var tenantScope = tenantAccessor.BeginScope(new TenantContext { TenantId = claim.TenantId });
        using var leaseCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeat = RenewLeaseAsync(store, claim, leaseCts);

        try
        {
            var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
            await engine.ProcessClaimedExecutionAsync(claim, leaseCts.Token);
        }
        catch (WorkflowExecutionLeaseLostException ex)
        {
            _logger.LogWarning(ex, "Worker {WorkerId} lost lease for workflow execution {ExecutionId}.", _workerId, claim.ExecutionId);
        }
        catch (OperationCanceledException) when (leaseCts.IsCancellationRequested)
        {
            _logger.LogInformation("Workflow execution {ExecutionId} stopped after lease loss or host shutdown.", claim.ExecutionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution {ExecutionId} failed in worker {WorkerId}.", claim.ExecutionId, _workerId);
        }
        finally
        {
            leaseCts.Cancel();
            try { await heartbeat; }
            catch (OperationCanceledException) { }
            await store.ReleaseExecutionLeaseAsync(claim, CancellationToken.None);
        }

        return true;
    }

    private async Task RenewLeaseAsync(IWorkflowStore store, WorkflowExecutionClaim claim, CancellationTokenSource leaseCts)
    {
        try
        {
            while (!leaseCts.IsCancellationRequested)
            {
                await Task.Delay(HeartbeatInterval, leaseCts.Token);
                if (!await store.RenewExecutionLeaseAsync(claim, LeaseDuration, leaseCts.Token))
                {
                    _logger.LogWarning("Lease renewal failed for workflow execution {ExecutionId}; cancelling worker.", claim.ExecutionId);
                    await leaseCts.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (leaseCts.IsCancellationRequested)
        {
            // The execution completed or the host is stopping.
        }
    }
}
