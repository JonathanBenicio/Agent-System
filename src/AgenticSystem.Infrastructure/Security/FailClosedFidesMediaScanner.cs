using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Security;

/// <summary>
/// Safe fallback used until a media OCR backend is configured.
/// Uninspected attachments are never forwarded to a provider.
/// </summary>
public sealed class FailClosedFidesMediaScanner : IFidesMediaScanner
{
    public Task<FidesMediaScanResult> ScanAndRedactAsync(
        ReadOnlyMemory<byte> content,
        string mediaType,
        FidesTenantPolicy policy,
        CancellationToken ct = default) =>
        Task.FromResult(new FidesMediaScanResult { Status = FidesMediaScanStatus.Uncertain });
}
