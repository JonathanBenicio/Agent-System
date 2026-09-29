#pragma warning disable MAAI001 // Required experimental MAF session-store integration; reviewed under issue #120.

using System.Text;
using AgenticSystem.Core.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using FrameworkAgent = Microsoft.Agents.AI.AIAgent;
using FrameworkAgentResponse = Microsoft.Agents.AI.AgentResponse;
using FrameworkSessionStore = Microsoft.Agents.AI.AgentSessionStore;
using FrameworkSessionStoreKey = Microsoft.Agents.AI.AgentSessionStoreKey;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Executa agentes nomeados no Microsoft Agent Framework sem criar wrappers transitórios de IAgent.
/// Mantém sessão, streaming opcional e fallback para o agente cru apenas em erro do framework.
/// </summary>
public class AgentFrameworkDirectExecutionService : IDirectAgentExecutionService
{
    private readonly AgentFrameworkFactory _frameworkFactory;
    private readonly FrameworkSessionStore _sessionStore;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<AgentFrameworkDirectExecutionService> _logger;
    private readonly IAgentRuntimeCoordinator? _runtimeCoordinator;
    private readonly IServiceProvider _serviceProvider;
    private readonly bool _enableStreaming;

    public AgentFrameworkDirectExecutionService(
        AgentFrameworkFactory frameworkFactory,
        FrameworkSessionStore sessionStore,
        ISessionManager sessionManager,
        ILogger<AgentFrameworkDirectExecutionService> logger,
        IServiceProvider serviceProvider,
        IAgentRuntimeCoordinator? runtimeCoordinator = null,
        bool enableStreaming = false)
    {
        _frameworkFactory = frameworkFactory ?? throw new ArgumentNullException(nameof(frameworkFactory));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _runtimeCoordinator = runtimeCoordinator;
        _enableStreaming = enableStreaming;
    }

    public async Task<AgentResponse> ExecuteDirectAsync(
        IAgent agent,
        string sessionId,
        string input,
        UserContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.UserId);

        agent.UpdateLastUsed();

        FrameworkAgent? frameworkAgent = null;

        try
        {
            var workflow = context.WorkflowOptions;
            Exception? workflowToolFailure = null;
            var allowedTools = workflow?.AllowedTools is { } restricted
                ? agent.AvailableTools.Intersect(restricted, StringComparer.OrdinalIgnoreCase).ToArray()
                : agent.AvailableTools.ToArray();
            var workflowTools = workflow is null ? null : CreateWorkflowTools(context, allowedTools,
                error => Interlocked.CompareExchange(ref workflowToolFailure, error, null));
            frameworkAgent = workflow is null
                ? await _frameworkFactory.CreateFromAgentAsync(agent, ct)
                : await _frameworkFactory.CreateFromAgentAsync(agent, workflowTools, workflow.Model, ct, allowedTools);
            var sessionKey = new FrameworkSessionStoreKey(sessionId).WithPartition(
                "isolation",
                $"{context.TenantId}:{context.UserId}");
            var session = await _sessionStore.GetOrCreateSessionAsync(frameworkAgent, sessionKey, ct);

            string content = string.Empty;
            FrameworkAgentResponse? frameworkResponse = null;

            if (_enableStreaming)
            {
                var sb = new StringBuilder();
                await foreach (var update in frameworkAgent.RunStreamingAsync(input, session).WithCancellation(ct))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        sb.Append(update.Text);
                        if (_runtimeCoordinator is not null)
                        {
                            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
                            {
                                Type = AgentStreamEventType.Token,
                                AgentName = agent.Name,
                                Message = update.Text
                            }, ct);
                        }
                    }
                }

                content = sb.ToString();
            }
            else
            {
                var schemaAttribute = agent.GetType().GetCustomAttributes(typeof(AgenticSystem.Core.Attributes.AgenticJsonSchemaAttribute), true).FirstOrDefault() as AgenticSystem.Core.Attributes.AgenticJsonSchemaAttribute;
                var validator = _serviceProvider.GetService(typeof(IStructuredOutputValidator)) as IStructuredOutputValidator;
                
                var runOptions = new Microsoft.Agents.AI.AgentRunOptions();
                if (schemaAttribute != null && !string.IsNullOrEmpty(schemaAttribute.CustomSchemaJson))
                {
                    try
                    {
                        var schemaElement = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(schemaAttribute.CustomSchemaJson);
                        runOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(
                            schemaElement,
                            schemaName: agent.Name,
                            schemaDescription: $"Schema constraints for {agent.Name}"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse CustomSchemaJson, falling back to ChatResponseFormat.Json");
                        runOptions.ResponseFormat = ChatResponseFormat.Json;
                    }
                }
                else if (schemaAttribute != null)
                {
                    runOptions.ResponseFormat = ChatResponseFormat.Json;
                }

                int maxRetries = schemaAttribute?.AutoRetryOnFailure == true ? schemaAttribute.MaxRetries : 1;
                string currentInput = input;
                
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    if (workflow?.ImagePath is { } imagePath)
                    {
                        var verifiedPath = VerifyTenantImagePath(imagePath, context.TenantId);
                        var imageBytes = await File.ReadAllBytesAsync(verifiedPath, ct);
                        if (imageBytes.Length > 10 * 1024 * 1024)
                            throw new InvalidOperationException("Workflow image exceeds 10 MiB.");
                        var mediaType = Path.GetExtension(verifiedPath).ToLowerInvariant() switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            ".webp" => "image/webp",
                            _ => throw new InvalidOperationException("Unsupported workflow image type.")
                        };
                        var message = new ChatMessage(ChatRole.User, currentInput);
                        message.Contents.Add(new DataContent(imageBytes, mediaType));
                        frameworkResponse = await frameworkAgent.RunAsync([message], session, options: runOptions, cancellationToken: ct);
                    }
                    else
                    {
                        frameworkResponse = await frameworkAgent.RunAsync(currentInput, session, options: runOptions, cancellationToken: ct);
                    }

                    content = string.Join("\n", frameworkResponse.Messages
                        .Where(m => m.Role == ChatRole.Assistant)
                        .SelectMany(m => m.Contents.OfType<TextContent>())
                        .Select(t => t.Text));

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        content = string.Join("\n", frameworkResponse.Messages
                            .Where(m => m.Role == ChatRole.Assistant)
                            .Select(m => m.Text));
                    }
                    
                    if (schemaAttribute != null && validator != null)
                    {
                        var schema = new StructuredOutputSchema
                        {
                            Name = agent.Name,
                            SchemaJson = schemaAttribute.CustomSchemaJson ?? "{}" // Basic fallback if NJsonSchema wasn't specified
                        };
                        
                        var validationResult = await validator.ValidateAsync(content, schema, ct);
                        if (!validationResult.IsValid)
                        {
                            _logger.LogWarning("Agent {AgentName} failed schema validation on attempt {Attempt}. Errors: {Errors}", agent.Name, attempt, string.Join(", ", validationResult.ValidationErrors));
                            if (attempt < maxRetries)
                            {
                                currentInput = $"The JSON is invalid against the schema. Fix the errors: {string.Join("; ", validationResult.ValidationErrors)}";
                                continue;
                            }
                        }
                    }
                    break;
                }
            }

            if (workflowToolFailure is not null)
                throw new InvalidOperationException("A workflow tool failed or was denied.", workflowToolFailure);

            var result = new AgentResponse
            {
                Content = content ?? string.Empty,
                AgentName = agent.Name,
                AgentTier = agent.Tier,
                Success = true,
                SessionId = sessionId,
                Metadata = new Dictionary<string, object>
                {
                    ["frameworkAgentId"] = frameworkAgent.Id ?? frameworkAgent.Name ?? agent.Name,
                    ["frameworkStreaming"] = _enableStreaming
                }
            };

            await SyncResponseAsync(sessionId, input, agent.Name, result);
            await _sessionStore.SaveSessionAsync(frameworkAgent, sessionKey, session, ct);

            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            _logger.LogWarning(ex, "Agent Framework direct execution failed for {Agent}, returning error response", agent.Name);

            if (_runtimeCoordinator is not null)
            {
                var data = new Dictionary<string, object>
                {
                    ["fallback"] = false
                };

                if (!string.IsNullOrWhiteSpace(frameworkAgent?.Id))
                {
                    data["frameworkAgentId"] = frameworkAgent!.Id!;
                }

                await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
                {
                    Type = AgentStreamEventType.Error,
                    AgentName = agent.Name,
                    Message = ex.Message,
                    Data = data
                }, ct);
            }

            return AgentResponse.Error($"Framework error: {ex.Message}", agent.Name);
        }
    }

    private async Task SyncResponseAsync(string sessionId, string userInput, string agentName, AgentResponse response)
    {
        var agentEvent = new AgentEvent
        {
            SessionId = sessionId,
            AgentName = agentName,
            UserInput = userInput,
            AgentResponse = response.Content,
            ActionsPerformed = response.ActionsPerformed,
            ToolsUsed = response.ToolsUsed,
            Context = new Dictionary<string, object>
            {
                ["source"] = "AgentFramework",
                ["success"] = response.Success,
                ["executionMode"] = "direct"
            }
        };

        await _sessionManager.AddEventAsync(sessionId, agentEvent);
    }

    private IReadOnlyList<AITool> CreateWorkflowTools(
        UserContext context, IReadOnlyCollection<string> allowedTools, Action<Exception> recordFailure)
    {
        if (!allowedTools.Contains("CleanImageAsync", StringComparer.OrdinalIgnoreCase)
            && !allowedTools.Contains("RenderBannerAsync", StringComparer.OrdinalIgnoreCase))
            return [];

        var skills = _serviceProvider.GetRequiredService<BannerProductionSkills>();
        var permissions = _serviceProvider.GetRequiredService<IPermissionService>();

        async Task AuthorizeAsync(string name, CancellationToken ct)
        {
            if (!await permissions.HasPermissionAsync(context.UserId, $"tools/{name}", Permission.Execute, ct))
                throw new UnauthorizedAccessException($"Workflow initiator cannot execute '{name}'.");
        }

        var result = new List<AITool>();
        if (allowedTools.Contains("CleanImageAsync", StringComparer.OrdinalIgnoreCase))
            result.Add(AIFunctionFactory.Create(
                async (string path, string items) =>
                {
                    try
                    {
                        await AuthorizeAsync("CleanImageAsync", CancellationToken.None);
                        var output = await skills.CleanImageAsync(VerifyTenantImagePath(path, context.TenantId), items);
                        return VerifyTenantImagePath(output, context.TenantId);
                    }
                    catch (Exception ex) { recordFailure(ex); throw; }
                },
                "CleanImageAsync", "Remove fios, postes e lixo de uma imagem do tenant"));
        if (allowedTools.Contains("RenderBannerAsync", StringComparer.OrdinalIgnoreCase))
            result.Add(AIFunctionFactory.Create(
                async (string path, decimal price, int beds, string location, string phone) =>
                {
                    try
                    {
                        await AuthorizeAsync("RenderBannerAsync", CancellationToken.None);
                        var output = await skills.RenderBannerAsync(
                            VerifyTenantImagePath(path, context.TenantId), price, beds, location, phone);
                        return VerifyTenantImagePath(output, context.TenantId);
                    }
                    catch (Exception ex) { recordFailure(ex); throw; }
                },
                "RenderBannerAsync", "Renderiza o banner final sobre uma imagem do tenant"));
        return result;
    }

    private string VerifyTenantImagePath(string imagePath, string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || Path.GetFileName(tenantId) != tenantId)
            throw new InvalidOperationException("Invalid tenant for workflow image access.");
        var host = _serviceProvider.GetRequiredService<IHostEnvironment>();
        var tenantDirectory = Path.GetFullPath(Path.Combine(host.ContentRootPath, "wwwroot", "uploads", tenantId));
        var path = Path.GetFullPath(imagePath);
        if (!path.StartsWith(tenantDirectory + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !File.Exists(path))
            throw new UnauthorizedAccessException("Workflow image must exist under the active tenant uploads directory.");
        return path;
    }
}
