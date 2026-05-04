namespace AgenticSystem.Core.Interfaces;

public interface ITool
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    ToolCategory Category { get; }
    bool RequiresAuth { get; }
    Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

public enum ToolCategory
{
    Calendar,
    Email,
    Storage,
    Notes,
    Tasks,
    Search,
    Api,
    Database
}

public record ToolInput
{
    public string Action { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
    public string? UserId { get; init; }
}

public record ToolResult
{
    public bool Success { get; init; }
    public object? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }

    public static ToolResult Ok(object? data = null) => new() { Success = true, Data = data };
    public static ToolResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}
