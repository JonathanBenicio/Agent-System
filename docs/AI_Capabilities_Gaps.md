# Diagnóstico de Capacidades AI/Agentic — Gaps e Oportunidades

> Gerado em: 2026-05-05  
> Projeto: AgenticSystem  
> Escopo: Capacidades **⚠️ subutilizadas** e **❌ não implementadas**

---

## Resumo Executivo

| Categoria | ✅ Usando bem | ⚠️ Parcial | ❌ Não usa | Cobertura |
|---|:---:|:---:|:---:|:---:|
| M.E.IA | 10 | 0 | 0 | 100% |
| M.Agent.Framework | 5 | 1 | 0 | 83% |
| RAG Pipeline | 12 | 0 | 0 | 100% |
| Skills & Tools | 7 | 0 | 0 | 100% |
| MCP | 6 | 0 | 0 | 100% |
| Jobs & Scheduling | 7 | 0 | 0 | 100% |
| Agents & Workflows | 10 | 0 | 0 | 100% |
| Integrações Externas | 5 | 0 | 0 | 100% |
| **Total** | **62** | **1** | **0** | **~98%** |

---

## ⚠️ Usando Parcialmente (1 item)

### 1. Multi-Agent Orchestration Nativa

| | |
|---|---|
| **Área** | Microsoft Agent Framework |
| **Status atual** | O fluxo principal já é framework-first: `AgentExecutionWorkflow.ExecuteAsync()` delega para `FrameworkOrchestratorService`, que resolve um `AIAgent` hosted + `AgentSessionStore` keyed; `AgentCollaborationWorkflow` já usa `AgentWorkflowBuilder.BuildSequential(...)`; A2A e AG-UI já estão expostos; `IAgentFactory` voltou a entregar agentes crus; o `AgentFrameworkAdapter` ficou explícito só no `ExecuteDirectAsync`; e o orquestrador já delega a montagem final do hosted agent e o catálogo de tools auxiliares para serviços dedicados |
| **Gap** | O gap deixou de ser “adotar workflows/A2A” e passou a ser **drenar as dívidas locais finais da migração**: enxugar a composição por sessão que ainda vive no `OrchestratorContextFactory`, adicionar testes de integração de protocolo e continuar usando extensões locais para reflection/quality gates enquanto o MAF não oferece isso nativamente |
| **Oportunidade** | Consolidar o hosting nativo fim-a-fim no fluxo principal, simplificar wrappers transitórios e expandir `BuildConcurrent`, checkpointing e outros recursos de workflow onde houver ROI real |
| **Impacto** | Médio — reduz código transitório e aproxima o runtime do modelo nativo já suportado pelo framework |
| **Esforço** | Médio-Alto — requer cortes incrementais em paths ainda acoplados ao modelo anterior |
| **Referência** | [`FrameworkOrchestratorService.cs`](../src/AgenticSystem.Infrastructure/AgentFramework/FrameworkOrchestratorService.cs), [`OrchestratorContextResolver.cs`](../src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextResolver.cs), [`OrchestratorContextFactory.cs`](../src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextFactory.cs), [`AgentFrameworkSessionStoreAdapter.cs`](../src/AgenticSystem.Infrastructure/AgentFramework/AgentFrameworkSessionStoreAdapter.cs), [`AgentCollaborationWorkflow.cs`](../src/AgenticSystem.Infrastructure/AI/AgentCollaborationWorkflow.cs), [`ServiceCollectionExtensions.cs`](../src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs), [`Program.cs`](../src/AgenticSystem.Api/Program.cs) |

**Decisão sugerida:** Tratar o fechamento completo deste item como remoção progressiva das dívidas locais finais da migração framework-first. Workflows e protocol hosting já fazem parte do runtime atual; o backlog remanescente é simplificar o orquestrador hosted e cortar código transitório à medida que cada slice for validado.

---

## ✅ Fechado Desde o Último Diagnóstico (17 itens)

| | |
|---|---|
| **Área** | RAG Pipeline |
| **Item** | Re-Ranker Neural com provider especializado real |
| **Status atual** | `LlmReRanker` resolve `ReRankingOptions` por tenant em runtime via `IRerankingSettingsAccessor`, tenta `IDedicatedReRankerProvider` antes do scorer por embeddings e suporta tanto `JinaReRankerProvider` quanto `LocalOnnxCrossEncoderReRankerProvider`; `/config` agora salva API key, parâmetros e assets por tenant com persistência compatível com os modos locais `InMemory` e `PostgreSQL`, aceitando upload individual de `model.onnx` / `vocab.txt` ou pacote `.zip` |
| **Resultado** | O gap de provider especializado foi fechado com uma trilha totalmente local forte e multitenant-safe; o rerank neural passa a operar como `Dedicated Provider → Embeddings → LLM`, com configuração dinâmica no front, empacotamento operacional básico via ZIP e sobrevivência a restart quando o storage mode é `PostgreSQL` |
| **Referência** | [`LlmReRanker.cs`](../src/AgenticSystem.Infrastructure/RAG/LlmReRanker.cs), [`JinaReRankerProvider.cs`](../src/AgenticSystem.Infrastructure/RAG/JinaReRankerProvider.cs), [`LocalOnnxCrossEncoderReRankerProvider.cs`](../src/AgenticSystem.Infrastructure/RAG/LocalOnnxCrossEncoderReRankerProvider.cs), [`RerankingSettingsAccessor.cs`](../src/AgenticSystem.Infrastructure/RAG/RerankingSettingsAccessor.cs), [`SettingsController.cs`](../src/AgenticSystem.Api/Controllers/SettingsController.cs), [`SettingsPage.tsx`](../frontend/src/components/settings/SettingsPage.tsx), [`ReRankingOptions.cs`](../src/AgenticSystem.Infrastructure/Configuration/ReRankingOptions.cs) |

---

| | |
|---|---|
| **Área** | RAG Pipeline |
| **Item** | Microsoft.Extensions.DataIngestion |
| **Status atual** | Dependência removida do projeto de infraestrutura |
| **Resultado** | Pipeline custom de ingestão permanece como implementação oficial, sem dead weight no `.csproj` |
| **Referência** | [`AgenticSystem.Infrastructure.csproj`](../src/AgenticSystem.Infrastructure/AgenticSystem.Infrastructure.csproj) |

---

| | |
|---|---|
| **Área** | RAG Pipeline |
| **Item** | Semantic Compression no retrieval |
| **Status atual** | `RAGService` aplica `ISemanticCompressor.CompressRankedChunksAsync()` quando o contexto excede o budget |
| **Resultado** | `RAGContext` agora expõe `EffectiveQuery`, `QueryVariants`, `SemanticSummary`, `UsedSemanticCompression` e `OriginalContextTokens` |
| **Referência** | [`RAGService.cs`](../src/AgenticSystem.Infrastructure/RAG/RAGService.cs), [`SemanticCompressorService.cs`](../src/AgenticSystem.Core/Services/SemanticCompressorService.cs) |

---

| | |
|---|---|
| **Área** | Jobs & Scheduling |
| **Item** | Retry com backoff e dead-letter |
| **Status atual** | `ScheduledTaskManager` suporta `maxRetryAttempts`, backoff exponencial, contagem de falhas consecutivas e dead-letter local |
| **Resultado** | Falhas repetidas deixam de ficar em retry infinito e podem ser retomadas com reset explícito de estado |
| **Referência** | [`ScheduledTaskManager.cs`](../src/AgenticSystem.Core/Services/ScheduledTaskManager.cs), [`MaturityModels.cs`](../src/AgenticSystem.Core/Models/MaturityModels.cs) |

---

| | |
|---|---|
| **Área** | Integrações Externas |
| **Item** | Hardening do webhook delivery |
| **Status atual** | `WebhookDeliveryChannel` suporta timeout, custom headers, idempotency header e assinatura HMAC SHA-256 via config dictionary |
| **Resultado** | O canal deixou de ser apenas um POST simples e passou a ter segurança e deduplicação no contrato de entrega |
| **Referência** | [`WebhookDeliveryChannel.cs`](../src/AgenticSystem.Core/Services/WebhookDeliveryChannel.cs) |

---

| | |
|---|---|
| **Área** | Microsoft.Extensions.AI |
| **Item** | Microsoft.Extensions.AI.Evaluation |
| **Status atual** | `RuntimeEvaluatorService` integra `FluencyEvaluator` e `RelevanceTruthAndCompletenessEvaluator` quando há `sessionId` e contexto de sessão |
| **Resultado** | O score heurístico continua existindo, mas agora é enriquecido por avaliação oficial e persistido no `IOperationalStore` |
| **Referência** | [`RuntimeEvaluatorService.cs`](../src/AgenticSystem.Core/Services/RuntimeEvaluatorService.cs), [`IOperationalStore.cs`](../src/AgenticSystem.Core/Interfaces/IOperationalStore.cs) |

---

| | |
|---|---|
| **Área** | Microsoft.Extensions.AI |
| **Item** | Middlewares custom no `IChatClient` |
| **Status atual** | `GovernedChatClient` decora o `ContextAwareChatClient` com limite de concorrência, timeout de fila e validação de request/response via `IQualityGateService` |
| **Resultado** | O pipeline de chat deixou de ser um pass-through simples e ganhou governança reutilizável sem refatorar o `LLMManager` |
| **Referência** | [`GovernedChatClient.cs`](../src/AgenticSystem.Infrastructure/LLM/GovernedChatClient.cs), [`ChatClientMiddlewareOptions.cs`](../src/AgenticSystem.Infrastructure/Configuration/ChatClientMiddlewareOptions.cs), [`ServiceCollectionExtensions.cs`](../src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs) |

---

| | |
|---|---|
| **Área** | MCP |
| **Item** | MCP Server Mode |
| **Status atual** | `Program.cs` registra `AddMcpServer().WithHttpTransport(...)` e expõe `app.MapMcp("/mcp").RequireAuthorization()` |
| **Resultado** | O sistema deixou de ser apenas MCP client e agora expõe tools autenticadas (`list_agents`, `search_knowledge`, `list_runtime_tools`, `execute_agent`) para outros agentes e clientes MCP |
| **Referência** | [`Program.cs`](../src/AgenticSystem.Api/Program.cs), [`AgenticMcpTools.cs`](../src/AgenticSystem.Api/MCP/AgenticMcpTools.cs) |

---

| | |
|---|---|
| **Área** | RAG Pipeline |
| **Item** | Microsoft.Extensions.VectorData / `AgenticVectorStoreAdapter` |
| **Status atual** | O adapter agora expõe `GetCollection<string, EmbeddingDocument>()`, `GetDynamicCollection()`, metadata de coleção, collection lifecycle lógico e operações de busca/recuperação/upsert/delete apoiadas no `IVectorStore` custom |
| **Resultado** | O runtime ganhou interoperabilidade real com `VectorStoreCollection` do ecossistema M.E.AI sem abandonar o store custom existente |
| **Referência** | [`AgenticVectorStoreAdapter.cs`](../src/AgenticSystem.Infrastructure/AI/AgenticVectorStoreAdapter.cs), [`IVectorStore.cs`](../src/AgenticSystem.Core/Interfaces/IVectorStore.cs), [`InMemoryVectorStore.cs`](../src/AgenticSystem.Infrastructure/Memory/InMemoryVectorStore.cs), [`PostgresVectorStore.cs`](../src/AgenticSystem.Infrastructure/Persistence/PostgresVectorStore.cs) |

---

| | |
|---|---|
| **Área** | RAG Pipeline |
| **Item** | Query Expansion com HyDE condicional |
| **Status atual** | `RAGService` mantém compressão/variantes heurísticas e agora gera uma variante HyDE via `IChatClient` quando o retrieval inicial vem fraco |
| **Resultado** | Queries ambíguas ou curtas ganham uma segunda chance de recall sem custo extra em todos os requests; o `RAGContext` passou a expor `UsedHydeExpansion` e `HydeVariant` |
| **Referência** | [`RAGService.cs`](../src/AgenticSystem.Infrastructure/RAG/RAGService.cs), [`RAGModels.cs`](../src/AgenticSystem.Core/Models/RAGModels.cs) |

---

| | |
|---|---|
| **Área** | Skills & Tools |
| **Item** | Skills dinâmicas via YAML/JSON |
| **Status atual** | `DynamicSkillCatalogHostedService` carrega skills declarativas de diretório configurável e registra no `ISkillManager`, preservando o seed das skills built-in |
| **Resultado** | O runtime passou a aceitar extensão de skills sem recompilação, com override opcional e resolução de diretório relativa ao content root ou à raiz do repositório |
| **Referência** | [`DynamicSkillCatalogHostedService.cs`](../src/AgenticSystem.Infrastructure/Skills/DynamicSkillCatalogHostedService.cs), [`DeclarativeSkill.cs`](../src/AgenticSystem.Infrastructure/Skills/DeclarativeSkill.cs), [`DynamicSkillsOptions.cs`](../src/AgenticSystem.Infrastructure/Configuration/DynamicSkillsOptions.cs) |

---

| | |
|---|---|
| **Área** | Jobs & Scheduling |
| **Item** | Task chaining / DAG-lite |
| **Status atual** | `ScheduledTaskManager` agora permite ligar tarefas via `LinkTasksAsync`, mantém `DependencyTaskIds`/`ContinuationTaskIds`, bloqueia ciclos e só libera continuações quando todos os predecessores concluíram com sucesso |
| **Resultado** | O scheduler passou a suportar encadeamento do tipo "terminou A, libera B, depois C" sem reescrever o runtime existente de CRON/intervalo e sem permitir grafos cíclicos |
| **Referência** | [`ScheduledTaskManager.cs`](../src/AgenticSystem.Core/Services/ScheduledTaskManager.cs), [`IScheduledTaskServices.cs`](../src/AgenticSystem.Core/Interfaces/IScheduledTaskServices.cs), [`MaturityModels.cs`](../src/AgenticSystem.Core/Models/MaturityModels.cs) |

---

| | |
|---|---|
| **Área** | Microsoft Agent Framework |
| **Item** | Agent-to-Agent Channels nativos via sessão estruturada |
| **Status atual** | `FrameworkAgentChannelService` persiste mensagens de canal na sessão e o orquestrador/workflows passam a construir inputs com contexto compartilhado entre agents |
| **Resultado** | Planner, delegações e reviewer deixaram de trocar apenas strings soltas e passaram a reutilizar um canal estruturado e persistido por sessão |
| **Referência** | [`FrameworkAgentChannelService.cs`](../src/AgenticSystem.Infrastructure/AgentFramework/FrameworkAgentChannelService.cs), [`FrameworkOrchestratorService.cs`](../src/AgenticSystem.Infrastructure/AgentFramework/FrameworkOrchestratorService.cs), [`AgentCollaborationWorkflow.cs`](../src/AgenticSystem.Infrastructure/AI/AgentCollaborationWorkflow.cs) |

---

| | |
|---|---|
| **Área** | Skills & Tools |
| **Item** | Tool Versioning / A/B Testing |
| **Status atual** | `InMemoryToolManager` agora mantém registry por logical tool id com versões, variantes, rollout percentual e seleção determinística por usuário/sessão |
| **Resultado** | O runtime ganhou base para rollout gradual de tool variants sem quebrar o contrato existente de `RegisterTool()` |
| **Referência** | [`InMemoryToolManager.cs`](../src/AgenticSystem.Core/Services/InMemoryToolManager.cs), [`IToolManager.cs`](../src/AgenticSystem.Core/Interfaces/IToolManager.cs), [`ToolVariantModels.cs`](../src/AgenticSystem.Core/Models/ToolVariantModels.cs) |

---

| | |
|---|---|
| **Área** | Agents & Workflows |
| **Item** | Agent Memory Per-Agent |
| **Status atual** | `BaseAgent` agora consulta `IAgentMemoryService` para enriquecer o system prompt com memórias relevantes por agente/usuário, com store in-memory e persistência EF opcional |
| **Resultado** | Cada agente passou a reutilizar fatos, regras aprendidas e correções entre sessões, sem depender apenas do histórico da sessão corrente |
| **Referência** | [`AgentMemoryService.cs`](../src/AgenticSystem.Core/Services/AgentMemoryService.cs), [`BaseAgent.cs`](../src/AgenticSystem.Core/Agents/BaseAgent.cs), [`EfAgentMemoryStore.cs`](../src/AgenticSystem.Infrastructure/Persistence/EfAgentMemoryStore.cs) |

---

| | |
|---|---|
| **Área** | Agents & Workflows |
| **Item** | Agent Self-Improvement |
| **Status atual** | `AgentExecutionWorkflow` converte reflexões críticas em regras automáticas via `ICorrectionLoop` e também persiste as sugestões na memória do agente |
| **Resultado** | O loop de melhoria deixou de depender apenas de regras manuais: falhas/reflexões passam a gerar aprendizados reutilizados em execuções futuras |
| **Referência** | [`AgentExecutionWorkflow.cs`](../src/AgenticSystem.Core/Services/AgentExecutionWorkflow.cs), [`CorrectionLoopService.cs`](../src/AgenticSystem.Core/Services/CorrectionLoopService.cs), [`AgentMemoryModels.cs`](../src/AgenticSystem.Core/Models/AgentMemoryModels.cs) |

---

| | |
|---|---|
| **Área** | Integrações Externas |
| **Item** | Calendar / Email / Notes / Storage Providers |
| **Status atual** | Providers locais/file-backed foram adicionados e registrados no DI para calendário, email, notas e storage |
| **Resultado** | As interfaces de integração deixaram de estar órfãs e agora possuem implementações funcionais para uso local/dev, com persistência em filesystem/JSON |
| **Referência** | [`LocalCalendarProvider.cs`](../src/AgenticSystem.Infrastructure/Integrations/LocalCalendarProvider.cs), [`LocalEmailProvider.cs`](../src/AgenticSystem.Infrastructure/Integrations/LocalEmailProvider.cs), [`ObsidianNotesProvider.cs`](../src/AgenticSystem.Infrastructure/Integrations/ObsidianNotesProvider.cs), [`LocalStorageProvider.cs`](../src/AgenticSystem.Infrastructure/Integrations/LocalStorageProvider.cs) |

---

## ❌ Não Implementado (0 itens)

Nenhum item do diagnóstico permanece totalmente sem implementação. O backlog aberto agora é evolutivo sobre o item ainda parcial de orquestração nativa e a expansão arquitetural do rerank local.

## Matriz de Priorização

Foco restante do roadmap:

- Médio impacto / alto esforço: consolidar o hosting nativo do orquestrador principal e cortar os wrappers transitórios que ainda sobraram fora do slice colaborativo.
- Médio impacto / médio esforço: expandir compatibilidade para mais arquiteturas de rerank além do fluxo atual de upload individual/ZIP e persistência por tenant.

## Roadmap Sugerido

### Sprint 1 — Quick Wins (⚠️ parciais → completos)

- [x] ⚠️1 — Middleware custom de rate limiting no `IChatClient` pipeline
- [x] Remover dependência `Microsoft.Extensions.DataIngestion` sem uso
- [x] Aplicar `SemanticCompressorService` no pipeline RAG
- [x] Adicionar retry com exponential backoff e dead-letter no scheduler
- [x] Harden webhook delivery com timeout, HMAC, idempotência e headers customizados
- [x] Evoluir query expansion heurística para HyDE / expansão baseada em LLM

### Sprint 2 — Avaliação e Observabilidade

- [x] Integrar `Microsoft.Extensions.AI.Evaluation`
- [x] Expor capabilities como MCP Server

### Sprint 3 — Memória e Aprendizado

- [x] Agent Memory Per-Agent (store in-memory + persistência EF opcional)
- [x] Re-Ranker Neural (cross-encoder ONNX local + provider dedicado + embeddings + LLM fallback opcional + configuração/upload persistidos por tenant, incluindo pacote ZIP com assets)

### Sprint 4 — Extensibilidade

- [x] Skills dinâmicas via YAML/config
- [x] Task chaining / DAG no scheduler
- [x] Completar `AgenticVectorStoreAdapter`

### Backlog

- [ ] Evoluir Multi-Agent Orchestration Nativa para `Microsoft.Agents.AI.Workflows`, com group chat, handoff e termination policies nativos
- [ ] Expandir o re-ranker dedicado para múltiplos modelos locais e mais arquiteturas de tokenizer além do suporte atual por upload no `/config` com arquivos individuais ou pacote ZIP
