using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using AgenticSystem.Core.Skills;
using AgenticSystem.Infrastructure.Configuration;

namespace AgenticSystem.Infrastructure.AI;

public class BannerProductionWorkflowService
{
    private readonly IChatClient _visionChatClient;
    private readonly IChatClient _actionChatClient;
    private readonly BannerProductionSkills _skills;

    public BannerProductionWorkflowService(BannerProductionSkills skills, IOptions<OllamaSettings> ollamaOptions)
    {
        _skills = skills;
        var settings = ollamaOptions.Value;
        var baseUrl = new Uri(settings.BaseUrl ?? "http://localhost:11434");
        _visionChatClient = new OllamaChatClient(baseUrl, "llama3.2-vision");
        _actionChatClient = new OllamaChatClient(baseUrl, "llama3-8b");
    }

    public async Task<string> RunBannerProductionAsync(string imagePath, decimal price, int bedrooms)
    {
        var visionAgent = new Microsoft.Agents.AI.ChatClientAgent(
            _visionChatClient,
            "Voce e um analista visual de imoveis. Descreva os defeitos da foto (fios, postes) focando no topo da imagem, e identifique os pontos fortes. Fale em portugues de forma concisa.",
            "VisionAnalyst",
            "Analista Visual"
        );

        // Convertendo as skills em AIFunctions do Microsoft.Extensions.AI
        var cleanFunc = AIFunctionFactory.Create(
            (string path, string items) => _skills.CleanImageAsync(path, items),
            "CleanImageAsync",
            "Remove fios e lixo de uma imagem"
        );
        
        var renderFunc = AIFunctionFactory.Create(
            (string path, decimal prc, int beds) => _skills.RenderBannerAsync(path, prc, beds),
            "RenderBannerAsync",
            "Gera o banner publicitário final"
        );

        var actionAgent = new Microsoft.Agents.AI.ChatClientAgent(
            _actionChatClient,
            "Voce recebe a analise visual. Siga ESTRITAMENTE estes passos na ordem: 1) Chame a ferramenta CleanImageAsync passando a analise. 2) Pegue o caminho da imagem limpa retornado e chame a ferramenta RenderBannerAsync passando o caminho limpo, o preco e os quartos. 3) Retorne ao usuario o resultado final com o caminho.",
            "EditorChefe",
            "Editor Chefe",
            new[] { cleanFunc, renderFunc }
        );

        var builder = new Microsoft.Agents.AI.Workflows.WorkflowBuilder(visionAgent);
        builder.BindExecutor(actionAgent);
        builder.AddEdge(visionAgent, actionAgent);
        builder.WithName("banner-production-workflow");
        
        var workflow = builder.Build();

        var prompt = new ChatMessage(
            ChatRole.User, 
            $"Analise a foto, aplique a remocao de defeitos e desenhe um banner. Preco: {price}, Quartos: {bedrooms}. O caminho original e: {imagePath}"
        );

        if (File.Exists(imagePath))
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var ext = Path.GetExtension(imagePath).TrimStart('.').ToLower();
            if (ext == "jpg") ext = "jpeg";
            prompt.Contents.Add(new DataContent(imageBytes, $"image/{ext}"));
        }

        var messages = new List<ChatMessage> { prompt };
        
        await using var run = await Microsoft.Agents.AI.Workflows.InProcessExecution.RunAsync(workflow, messages, "banner-run", CancellationToken.None);
        
        var lastEvent = run.OutgoingEvents
            .OfType<Microsoft.Agents.AI.Workflows.AgentResponseEvent>()
            .LastOrDefault();

        if (lastEvent?.Response != null)
        {
            var msgsProp = lastEvent.Response.GetType().GetProperty("Messages");
            if (msgsProp?.GetValue(lastEvent.Response) is IEnumerable<ChatMessage> msgs)
            {
                var lastAssistant = msgs.LastOrDefault(m => m.Role == ChatRole.Assistant);
                return lastAssistant?.Text ?? "Sucesso (sem texto nas mensagens).";
            }
            
            return lastEvent.Response.ToString() ?? "Sucesso (retorno desconhecido).";
        }

        return "Processamento concluído, mas sem mensagem de saída legível.";
    }
}
