using System.Threading;
using System.Threading.Tasks;

namespace AgenticSystem.Core.Services.FastPath
{
  public interface IFastPathInterceptor
  {
    Task<(bool IsFastPath, string? Response)> EvaluateAsync(string input, CancellationToken cancellationToken = default);
  }
}