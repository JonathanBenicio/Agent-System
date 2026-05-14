# Plano de Estabilização e Self-Hosting com Ollama

Este documento define a análise profunda de gaps e o plano de ação arquitetural para tornar o Agent-System 100% autônomo e auto-hospedado (self-hosted) utilizando o Ollama como provedor primário e exclusivo de LLM e Embeddings Vetoriais.

---

## Fase 1: Análise Profunda & Identificação de Gaps

A inspeção detalhada da infraestrutura atual de injeção de dependência (`ServiceCollectionExtensions.cs`), orquestração (`docker-compose.yml`) e configurações iniciais (`appsettings.json`) revelou três gaps críticos que impedem a execução puramente local com Ollama:

### Gap 1: Injeção de Dependência Ausente para Embeddings Vetoriais (RAG/Memória)
- **Cenário Atual**: Em `ServiceCollectionExtensions.cs`, a interface `IEmbeddingGenerator<string, Embedding<float>>` da biblioteca `Microsoft.Extensions.AI` é registrada de forma acoplada ao cliente da OpenAI (`OpenAI.Embeddings.EmbeddingClient`), condicionado à existência de uma `ApiKey` da OpenAI.
- **Ponto de Falha (Gap)**: Se o usuário não fornece uma chave OpenAI e ativa apenas o Ollama, nenhum gerador vetorial global é registrado no contêiner de injeção de dependência. Ao inicializar serviços de RAG, Re-ranking ou Obsidian Sync, o sistema dispara uma exceção fatal de dependência ausente (`InvalidOperationException`).

### Gap 2: Inicialização de Modelos Frios no Docker (Ollama Pull)
- **Cenário Atual**: O serviço `ollama` no `docker-compose.yml` sobe utilizando a imagem base limpa (`ollama/ollama:latest`), expondo a porta `11434`.
- **Ponto de Falha (Gap)**: Quando os contêineres sobem, nenhum modelo de LLM (ex: `llama3`) ou de embedding (ex: `nomic-embed-text`) é baixado nativamente. Quando a API do C# inicializa e tenta validar o health ou executar um fluxo de chat/memória, o Ollama rejeita a requisição informando `model not found`.

### Gap 3: Configurações Primárias e Prioridades em `appsettings.json`
- **Cenário Atual**: O `appsettings.json` possui a seção OpenAI ativa com prioridade máxima (`Priority: 1`), e não possui a seção Ollama explícita.
- **Ponto de Falha (Gap)**: Para um ambiente 100% self-hosted, o `LLMManager` precisa que o Ollama possua prioridade superior e que as variáveis de ambiente definam o Ollama como o provedor padrão de chat e embedding.

---

## Fase 2: Planejamento & Tarefas

| ID | Tarefa | Escopo / Arquivo Alvo | Critérios de Aceitação |
|---|---|---|---|
| **T1** | Roteamento de Embedding Vetorial | `ServiceCollectionExtensions.cs` | Registrar dinamicamente um `IEmbeddingGenerator` baseado no `OllamaEmbeddingGenerator` quando o Ollama estiver habilitado e a OpenAI ausente/desativada. |
| **T2** | Orquestração de Boot Frio (Ollama Pull) | `docker-compose.yml` | Adicionar um contêiner temporário (`ollama-pull`) que execute `ollama pull llama3` e `ollama pull nomic-embed-text` antes de liberar o tráfego da API. |
| **T3** | Padronização de Configurações | `appsettings.json` / `docker-compose.yml` | Documentar e incluir as seções nativas de Ollama e LLM Default Provider, definindo o Ollama com prioridade 1 no modo self-hosted. |

---

## Fase 3: Arquitetura da Solução (Solutioning)

### 1. Injeção de Dependência Desacoplada (T1)
Modificar o bloco de registro de embeddings no `ServiceCollectionExtensions.cs`:
```csharp
var ollamaSettings = configuration.GetSection("AgenticSystem:Ollama");
var ollamaEnabled = bool.TryParse(ollamaSettings["Enabled"], out var oe) ? oe : false;
var openAiApiKey = configuration["AgenticSystem:OpenAI:ApiKey"];

if (!string.IsNullOrWhiteSpace(openAiApiKey))
{
    // Registra OpenAI Embedding
}
else if (ollamaEnabled)
{
    var ollamaBaseUrl = ollamaSettings["BaseUrl"] ?? "http://localhost:11434";
    var ollamaModel = ollamaSettings["EmbeddingModel"] ?? "nomic-embed-text";
    
    services.AddEmbeddingGenerator(_ => 
        new Microsoft.Extensions.AI.OllamaEmbeddingGenerator(new Uri(ollamaBaseUrl), ollamaModel))
        .UseDistributedCache();
}
```

### 2. Inicialização Estável via Docker (T2)
Adicionar o serviço de pré-carregamento no `docker-compose.yml`:
```yaml
ollama-pull:
  image: curlimages/curl:latest
  command: >
    sh -c "
      echo 'Aguardando Ollama iniciar...';
      while ! curl -s http://ollama:11434/api/tags > /dev/null; do sleep 2; done;
      echo 'Baixando modelo LLM primário (llama3)...';
      curl -X POST http://ollama:11434/api/pull -d '{\"name\":\"llama3\"}';
      echo 'Baixando modelo de Embeddings (nomic-embed-text)...';
      curl -X POST http://ollama:11434/api/pull -d '{\"name\":\"nomic-embed-text\"}';
      echo 'Modelos prontos e inicializados!';
    "
  depends_on:
    ollama:
      condition: service_started
  networks:
    - agentic-net
```

---

## Fase 4: Implementação & Próximos Passos

O plano está pronto e gravado em `docs/plan/p4-selfhost-ollama-stabilization.md`. Aguardamos a aprovação do usuário para iniciarmos a aplicação das alterações nos arquivos do projeto.
