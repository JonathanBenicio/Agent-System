using System.Threading;
using System.Threading.Tasks;

namespace AgenticSystem.Core.Interfaces;

public interface IFastPathInterceptor
{
    Task<(bool IsFastPath, string? Response)> EvaluateAsync(string input, CancellationToken cancellationToken = default);
}
