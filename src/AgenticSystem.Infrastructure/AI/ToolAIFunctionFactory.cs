using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.AI;

/// <summary>
/// Converte ITool registrados em AIFunction (M.E.AI) para uso com FunctionInvocationMiddleware.
/// Substitui ToolKernelPluginFactory (SK) — cada ITool vira um AITool invocável pelo IChatClient pipeline.
/// </summary>
public static class ToolAIFunctionFactory
{
    /// <summary>
    /// Cria AITools a partir de todos os ITool registrados.
    /// Cada tool se torna um AIFunction com parâmetros action + parametersJson.
    /// </summary>
    public static IReadOnlyList<AITool> CreateFromTools(IEnumerable<ITool> tools, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ToolAIFunction");
        var aiFunctions = new List<AITool>();

        foreach (var tool in tools)
        {
            var function = CreateAIFunctionFromTool(tool, logger);
            aiFunctions.Add(function);
        }

        logger.LogDebug("ITool → AIFunction: {Count} tools disponíveis", aiFunctions.Count);
        return aiFunctions;
    }

    private static AIFunction CreateAIFunctionFromTool(ITool tool, ILogger logger)
    {
        return AIFunctionFactory.Create(
            async (string action, string? parametersJson, CancellationToken ct) =>
            {
                var parameters = string.IsNullOrEmpty(parametersJson)
                    ? new Dictionary<string, object>()
                    : JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson)
                      ?? new Dictionary<string, object>();

                var input = new ToolInput
                {
                    Action = action,
                    Parameters = parameters
                };

                logger.LogDebug("🔧 AIFunction → ITool: {Tool}/{Action}", tool.Name, action);
                var result = await tool.ExecuteAsync(input, ct);

                return result.Success
                    ? JsonSerializer.Serialize(result.Data)
                    : $"Error: {result.ErrorMessage}";
            },
            new AIFunctionFactoryOptions
            {
                Name = tool.Id,
                Description = tool.Description
            });
    }
}
