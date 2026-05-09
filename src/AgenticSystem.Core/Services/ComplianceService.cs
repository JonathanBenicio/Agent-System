using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class ComplianceService : IComplianceService
{
    private readonly ISessionStore _sessionStore;
    private readonly IAuditLog _auditLog;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<ComplianceService> _logger;
    private readonly List<RetentionPolicy> _policies = new();
    private readonly List<DataSubjectRequest> _requests = new();

    public ComplianceService(
        ISessionStore sessionStore,
        IAuditLog auditLog,
        IVectorStore vectorStore,
        ILogger<ComplianceService> logger)
    {
        _sessionStore = sessionStore;
        _auditLog = auditLog;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public Task<RetentionPolicy> SetRetentionPolicyAsync(RetentionPolicy policy, CancellationToken ct = default)
    {
        _logger.LogInformation("🛡️ Setting retention policy: {Name} (Scope: {Scope})", policy.Name, policy.Scope);
        lock (_policies)
        {
            _policies.RemoveAll(p => p.TenantId == policy.TenantId && p.Scope == policy.Scope);
            _policies.Add(policy);
        }
        return Task.FromResult(policy);
    }

    public Task<RetentionPolicy?> GetRetentionPolicyAsync(string? tenantId = null, CancellationToken ct = default)
    {
        lock (_policies)
        {
            return Task.FromResult(_policies.FirstOrDefault(p => p.TenantId == tenantId));
        }
    }

    public Task<DataSubjectRequest> SubmitDataRequestAsync(DataSubjectRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("📩 Data Subject Request received: {Type} for {SubjectId}", request.RequestType, request.SubjectId);
        lock (_requests)
        {
            _requests.Add(request);
        }
        return Task.FromResult(request);
    }

    public Task<DataSubjectRequest> ProcessDataRequestAsync(string requestId, CancellationToken ct = default)
    {
        lock (_requests)
        {
            var request = _requests.FirstOrDefault(r => r.Id == requestId);
            if (request == null) throw new ArgumentException("Request not found");
            
            request.Status = DataSubjectRequestStatus.Completed;
            request.CompletedAt = DateTime.UtcNow;
            return Task.FromResult(request);
        }
    }

    public async Task<int> EnforceRetentionAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("🧹 Enforcing data retention policies.");
        int deletedCount = 0;

        foreach (var policy in _policies)
        {
            if (ct.IsCancellationRequested) break;

            if (policy.Scope == RetentionScope.ConversationHistory && policy.ActionOnExpiry == RetentionAction.Delete)
            {
                var cutoff = DateTime.UtcNow - policy.RetentionPeriod;
                _logger.LogDebug("Cleanup sessions older than {Cutoff}", cutoff);
                // Implementation would call sessionStore.DeleteOlderThan(cutoff)
            }
        }

        return await Task.FromResult(deletedCount);
    }
}
