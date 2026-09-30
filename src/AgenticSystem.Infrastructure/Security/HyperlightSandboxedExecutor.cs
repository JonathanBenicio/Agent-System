using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Agents.AI.Hyperlight;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Security;

public interface IHyperlightCodeActRunner
{
    Task<string> ExecuteAsync(string code, CancellationToken ct = default);
}

public sealed class HyperlightCodeActRunner : IHyperlightCodeActRunner, IDisposable
{
    private readonly HyperlightExecuteCodeFunction _executeCode = new(
        HyperlightCodeActProviderOptions.CreateForJavaScript());

    public async Task<string> ExecuteAsync(string code, CancellationToken ct = default)
    {
        var arguments = new AIFunctionArguments { ["code"] = code };
        var result = await _executeCode.InvokeAsync(arguments, ct);
        return result?.ToString() ?? string.Empty;
    }

    public void Dispose() => _executeCode.Dispose();
}

/// <summary>
/// Executes JavaScript through the official Hyperlight-backed CodeAct function.
/// This integration is available only when the Lab feature flag is enabled.
/// </summary>
public sealed class HyperlightSandboxedExecutor
{
    private readonly ILogger<HyperlightSandboxedExecutor> _logger;
    private readonly IHyperlightCodeActRunner _runner;
    private readonly bool _enabled;

    public HyperlightSandboxedExecutor(
        IOptions<HyperlightExecutionSettings> options,
        IHostEnvironment environment,
        IHyperlightCodeActRunner runner,
        ILogger<HyperlightSandboxedExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runner = runner;
        _enabled = options.Value.Enabled && environment.IsEnvironment("Lab");
    }

    public bool IsAvailable => _enabled;

    public async Task<SandboxedExecutionResult> ExecuteCodeAsync(
        string language,
        string code,
        CancellationToken ct = default)
    {
        if (!_enabled)
            return SandboxedExecutionResult.Fail("Hyperlight is disabled or unavailable outside the Lab environment.");

        if (string.IsNullOrWhiteSpace(code))
            return SandboxedExecutionResult.Fail("Code cannot be empty.");

        if (!language.Equals("javascript", StringComparison.OrdinalIgnoreCase) &&
            !language.Equals("js", StringComparison.OrdinalIgnoreCase))
            return SandboxedExecutionResult.Fail("The configured Hyperlight backend supports JavaScript only.");

        if (!AuditCodeSafety(code, out var violationReason))
        {
            _logger.LogWarning("Hyperlight safety gate blocked code: {Reason}", violationReason);
            return SandboxedExecutionResult.Fail($"Code rejected by the Hyperlight safety gate: {violationReason}");
        }

        try
        {
            var output = await _runner.ExecuteAsync(code, ct);
            return new SandboxedExecutionResult
            {
                Success = true,
                Output = output?.ToString() ?? string.Empty
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hyperlight JavaScript sandbox execution failed.");
            return SandboxedExecutionResult.Fail("Hyperlight sandbox execution failed.");
        }
    }

    private static bool AuditCodeSafety(string code, out string violationReason)
    {
        string[] forbiddenPatterns =
        [
            @"\bprocess\s*\.",
            @"\bchild_process\b",
            @"\brequire\s*\(",
            @"\bimport\s*(?:\(|[^\w])",
            @"\beval\s*\(",
            @"\bFunction\s*\(",
            @"\bfetch\s*\(",
            @"\bXMLHttpRequest\b",
            @"\bDeno\s*\.",
            @"\bBun\s*\."
        ];

        foreach (var pattern in forbiddenPatterns)
        {
            if (Regex.IsMatch(code, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                violationReason = $"Forbidden capability pattern '{pattern}'";
                return false;
            }
        }

        violationReason = string.Empty;
        return true;
    }

}

public sealed class SandboxedExecutionResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string? Error { get; init; }

    public static SandboxedExecutionResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Tool adapter for the Hyperlight CodeAct function. It never reports simulated output.
/// </summary>
public sealed class HyperlightExecuteCodeTool : ITool
{
    private readonly HyperlightSandboxedExecutor _executor;

    public HyperlightExecuteCodeTool(HyperlightSandboxedExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public string Id => "hyperlight-execute-code";
    public string Name => "Hyperlight Sandboxed Executor";
    public string Description => "Executes JavaScript using the Hyperlight sandbox in Lab.";
    public ToolCategory Category => ToolCategory.AI;
    public bool RequiresAuth => true;

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        var language = input.Parameters.TryGetValue("language", out var langObj)
            ? langObj?.ToString() ?? "javascript"
            : "javascript";
        var code = input.Parameters.TryGetValue("code", out var codeObj)
            ? codeObj?.ToString() ?? string.Empty
            : string.Empty;

        var result = await _executor.ExecuteCodeAsync(language, code, ct);
        if (!result.Success)
            return ToolResult.Fail(result.Error ?? "Hyperlight execution failed.");

        return ToolResult.Ok(new { output = result.Output });
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(_executor.IsAvailable);
}
