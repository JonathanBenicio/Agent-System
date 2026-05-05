using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface ILLMRuntimeContextAccessor
{
    LLMRuntimeContext? Current { get; }
    IDisposable BeginScope(UserContext userContext, string? sessionId = null);
}
