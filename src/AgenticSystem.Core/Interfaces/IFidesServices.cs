using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IFidesTenantPolicyStore
{
    Task<FidesTenantPolicy> GetAsync(CancellationToken ct = default);
    Task<FidesTenantPolicy> SaveAsync(
        IReadOnlyDictionary<string, bool> enabledDetectors,
        string updatedBy,
        CancellationToken ct = default);
}

public interface IFidesMediaScanner
{
    Task<FidesMediaScanResult> ScanAndRedactAsync(
        ReadOnlyMemory<byte> content,
        string mediaType,
        FidesTenantPolicy policy,
        CancellationToken ct = default);
}
