using System.Threading.Tasks;

namespace AgenticSystem.Core.Interfaces;

public interface IOnnxEventBroadcaster
{
    Task BroadcastJobStatusAsync(string tenantId, string jobId, string status, string? error = null, long? latencyMs = null, string? outputImagePath = null);
}
