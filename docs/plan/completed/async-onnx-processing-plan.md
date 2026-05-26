# Roadmap: Processamento Assíncrono de Inferência ONNX e Galeria de Resultados

> **Status documental:** Planejamento futuro / Aprovado
> **Escopo:** Implementação de fila local de processamento (Channels), worker de background (.NET BackgroundService), SignalR Hub para atualizações em tempo real e Galeria no React frontend.
> **Fonte de verdade operacional:** [ADR-023](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/023-async-onnx-processing.md), [US-43](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/user-stories/us-async-onnx-processing.md), [Issue #81](https://github.com/JonathanBenicio/Agent-System/issues/81)
> **Gerado em:** 22 de Maio de 2026
> **Projeto:** AgenticSystem

---

## Objetivo

Mover o processamento de testes de inferência ONNX do fluxo síncrono HTTP (bloqueante) para uma fila de execução assíncrona em background. Isso elimina os erros de `504 Gateway Timeout` gerados pelo Nginx ao rodar modelos pesados de upscaling em CPU local, e fornece ao usuário uma experiência premium de galeria de resultados com notificações pop-up em tempo real via SignalR.

## Princípios de Implantação

1. **Paralelismo Controlado:** O background worker deve processar 1 inferência de CPU por vez para evitar esgotamento de recursos físicos (CPU/Memória) no contêiner Docker do backend.
2. **Multi-Tenant Estrito:** Todo arquivo de imagem de entrada/saída deve residir em diretórios nomeados com o `TenantId` da requisição original, e a listagem da galeria deve filtrar estritamente por tenant.
3. **Resiliência e Fallback:** Se a fila estiver cheia ou ocorrer falha, o job deve ser persistido como `Failed` com o stack trace do erro capturado para auditoria técnica.
4. **Acoplamento Mínimo:** A fila local deve usar `System.Threading.Channels` nativos em memória, eliminando a dependência imediata de brokers externos de fila complexos (RabbitMQ, Redis, Hangfire).

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Modelagem & Banco | Definir entidade `CustomOnnxInferenceJobEntity`, relacionamentos, e aplicar migration no banco de dados. |
| 2 | Infraestrutura de Fila | Criar gerenciador de fila assíncrona e registrá-lo no DI (Dependency Injection). |
| 3 | Background Service | Implementar o `OnnxInferenceBackgroundWorker` para realizar a inferência assíncrona. |
| 4 | SignalR Hub | Criar o `OnnxHub` ou adicionar eventos de notificações de job no `WorkflowHub` e publicar atualizações. |
| 5 | Refatoração de APIs | Alterar o `OnnxModelController` para retornar `202 Accepted` de imediato e expor listagem/CRUD da galeria. |
| 6 | Frontend React | Criar visualizadores de status, notificações toast e a galeria de imagens geradas. |

---

## Detalhamento: Fila e Background Inference

### Por que implementar?
Reduzir latência percebida do usuário a zero ao enviar inferências, remover erros de timeout síncronos do gateway Nginx e possibilitar auditoria permanente de inferências.

### Arquitetura-alvo

```
[React Frontend] (Executar Teste)
       │
       ▼ (HTTP POST /api/onnx/models/{id}/test)
[OnnxModelController] 
       │
       ├─► [Database] (Persiste Job como 'Pending')
       │
       ├─► [In-Memory Channel Queue] (System.Threading.Channels)
       │
       ▼ (Retorna imediato 202 Accepted com JobId)
[React Frontend] (Exibe spinner de background e libera tela)

──◄ Workers consomem da fila assincronamente ◄──

[OnnxInferenceBackgroundWorker] (Hosted BackgroundService)
       │
       ├─► (De-queue próximo Job)
       ├─► (Altera status para 'Processing')
       ├─► (Carrega sessão ONNX & roda inferência em CPU)
       ├─► (Salva arquivo wwwroot/onnx-results/{tenant}/{jobId}.png)
       ├─► [Database] (Persiste Job como 'Completed' ou 'Failed')
       │
       ▼ (Dispara notificação via SignalR)
[SignalR HubContext] ────► (Evento 'JobCompleted' via WebSockets) ────► [React Frontend] (Exibe Toast / Pop-up de Concluído)
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `CustomOnnxInferenceJobEntity` | Entidade persistida no PostgreSQL representando o job de inferência. |
| `IOnnxInferenceQueue` / `OnnxInferenceQueue` | Interface e classe concreta gerenciando a fila do canal `System.Threading.Channels.Channel`. |
| `OnnxInferenceBackgroundWorker` | `BackgroundService` em loop infinito escutando novas requisições da fila e executando-as em segundo plano. |
| `OnnxHub` | Hub do SignalR para tráfego e broadcasting de atualizações de jobs aos respectivos grupos de tenants. |
| `OnnxModelController` | Controller atualizado com listagem de jobs, download de imagens e submissão não bloqueante de testes. |

### Plano por etapas

1. **Modelagem:**
   - Adicionar `CustomOnnxInferenceJobEntity` no projeto `AgenticSystem.Infrastructure`.
   - Adicionar relacionamento de chave estrangeira com `CustomOnnxModelEntity`.
   - Gerar e aplicar a migração correspondente.
2. **Criação da Fila Assíncrona:**
   - Criar estrutura `OnnxInferenceJobRequest` contendo ID do Job, ID do modelo, ID do Tenant, largura, altura, canais e os bytes originais da imagem.
   - Criar `OnnxInferenceQueue` registrando-a como `Singleton` na injeção de dependências.
3. **Desenvolvimento do Worker:**
   - Criar `OnnxInferenceBackgroundWorker` herdando de `BackgroundService`.
   - Consumir em loop usando `await queue.Reader.ReadAsync(ct)`.
   - Integrar com o `DynamicOnnxProcessorTool` para pré-processamento de imagens, inferência da sessão cacheada, e geração da imagem de saída em `/wwwroot/onnx-results/{tenant}/{jobId}.png`.
4. **Integração SignalR:**
   - Adicionar métodos no hub para permitir que conexões se registrem por `TenantId`.
   - Injetar `IHubContext` no BackgroundWorker para disparar os eventos de progresso e resultado.
5. **Desenvolvimento do Frontend:**
   - Conectar o frontend ao Hub do SignalR.
   - Modificar modal de teste para capturar o ID do Job do retorno 202 e escutar pelo SignalR.
   - Criar aba "Galeria de Resultados" no painel de modelos ONNX.

### Critérios de Aceite e SLOs
* [ ] **Sem Gateway Timeouts (504):** A submissão de testes ONNX tem tempo de resposta de API inferior a **100ms** (SLO).
* [ ] **Isolamento de Tenants:** É impossível para um tenant visualizar jobs ou baixar imagens de outro tenant.
* [ ] **Robustez de CPU:** O Background Worker gerencia no máximo 1 thread concorrente de inferência por padrão, blindando a API contra lentidões globais.

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| **Esgotamento de Disco (Disk Full)** | Criar política de retenção que expira e limpa fisicamente arquivos de resultados antigos (com mais de 7 dias) via background worker agendado. |
| **Queda do Servidor com Fila Cheia** | Como o canal está em memória, jobs pendentes são perdidos se o contêiner morrer. Para mitigar, na inicialização do servidor, o Worker buscará no banco todos os jobs marcados como `Pending` ou `Processing` e os reinserirá na fila assíncrona. |
| **Erros de Concorrência no DB Context** | O worker opera em escopo singleton, mas o `AgenticDbContext` é escopo transiente. Devemos injetar `IServiceScopeFactory` no worker para resolver um dbContext novo para cada ciclo de execução de Job. |
