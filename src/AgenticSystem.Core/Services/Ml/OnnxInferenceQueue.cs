using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AgenticSystem.Core.Services.Ml;

public record OnnxInferenceJobRequest(
    string JobId,
    string TenantId,
    string ModelId,
    string InputImagePath
);

public interface IOnnxInferenceQueue
{
    ValueTask EnqueueJobAsync(OnnxInferenceJobRequest job, CancellationToken cancellationToken = default);
    ValueTask<OnnxInferenceJobRequest> DequeueJobAsync(CancellationToken cancellationToken = default);
}

public class OnnxInferenceQueue : IOnnxInferenceQueue
{
    private readonly Channel<OnnxInferenceJobRequest> _queue;

    public OnnxInferenceQueue()
    {
        // Bounded to 1000 items, blocking writers when full.
        // SingleReader = true because we have exactly 1 sequentially processing background service.
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _queue = Channel.CreateBounded<OnnxInferenceJobRequest>(options);
    }

    public ValueTask EnqueueJobAsync(OnnxInferenceJobRequest job, CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<OnnxInferenceJobRequest> DequeueJobAsync(CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}
