# Plano de Refatoração: Aproximar Runtime do MAF Nativo

> **[TRANSITIONAL]** Este documento registra a trilha de redução de código transitório do runtime MAF.
> Fases já concluídas devem ser lidas como histórico de execução; apenas backlog residual e critérios de rollout seguem ativos.

**Data:** 7 de maio de 2026  
**Objetivo:** Reduzir código transitório de integração com MAF mantendo diferencial de produto  
**Duração estimada:** 3-4 sprints incrementais  
**Risco:** Baixo (cada fase é independente e reversível)

---

## 1. Situação Atual

### Componentes Nativo do MAF já em uso
✅ `AddAIAgent()` com hosting DI  
✅ `AgentSessionStore` keyed resolvido  
✅ `AgentWorkflowBuilder.BuildSequential`  
✅ `InProcessExecution.RunAsync`  
✅ `ChatClientAgent` com pipeline logging + telemetry  
✅ `AIContextProvider` (RAGContextProvider)  
✅ `IQualityGateService` integrado no builder  
✅ `AddAIAgent("AgenticSystem")` apontando para o hosted orchestrator no path de protocolo  

### Dívidas Transitórias (Local/Custom)
❌ `OrchestratorContextFactory` — composição manual por sessão  
❌ `OrchestratorHostBuilder` + `OrchestratorContextFactory` — composição hosted ainda local por sessão  

### Customização Permanente do Produto (não mover)
✅ `IAgentExecutionPreProcessingPipeline` — validação + correction rules (diferencial)  
✅ `IAgentExecutionPostProcessingPipeline` — reflection + approval + memory (diferencial)  
✅ `OrchestratorAuxiliaryTools` — SmartRouter + ContextAnalyzer + RAG tools (estratégia)  
✅ `GovernedChatClient` — middleware de governança (proteção)  
✅ `FrameworkAgentChannelService` — collaboração estruturada (UX)  

---

## 2. Fases de Refatoração

### Fase 1: Consolidar OrchestratorContextFactory em AddAIAgent declarativo
**Impacto:** Alto | **Esforço:** Médio | **Risco:** Baixo  
**Objetivo:** Mover composição de sessão para o padrão nativo de host builder

**Mudanças:**
1. Criar `OrchestratorHostBuilder` que encapsula as 5 operações de montagem
2. Simplificar `OrchestratorContextFactory.Resolve()` para apenas desencadear builder
3. Remover `OrchestratorContext` record transitório (passar direto `ChatClientAgent`)

**Antes:**
```csharp
var orchestratorCtx = scopedServices.GetRequiredService<OrchestratorContext>();
var orchestrator = scopedServices.GetRequiredKeyedService<AIAgent>(...);
var session = await sessionStore.GetSessionAsync(orchestrator, sessionId, ct);
```

**Depois:**
```csharp
var orchestrator = scopedServices.GetRequiredKeyedService<AIAgent>(...);
var session = await _sessionStore.GetSessionAsync(orchestrator, sessionId, ct);
// Composição de context não é mais necessária — o builder já resolveu
```

**Referência de código:**
- [src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextFactory.cs](src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextFactory.cs)

---

### Fase 2: Consolidada — Session store final no SimpleSessionStoreAdapter
**Impacto:** Médio | **Esforço:** Médio | **Risco:** Médio (requer testes)  
**Objetivo:** Reduzir fallback de chaves legadas e deduplicação de lógica

**Mudanças:**
1. Remover `FrameworkSessionStateKeyResolver` — usar apenas nome do agente
2. Eliminar recuperação por eventos históricos (usar `ISessionStore` principal)
3. Simplificar `SaveSessionAsync` e `GetSessionAsync` para 1:1 mapping
4. Remover o adapter legado do runtime após validar o store simples

**Antes:** ~220 linhas com 3 estratégias de chave  
**Depois:** ~80 linhas com 1 estratégia  

**Referência de código:**
- [src/AgenticSystem.Infrastructure/AgentFramework/SimpleSessionStoreAdapter.cs](src/AgenticSystem.Infrastructure/AgentFramework/SimpleSessionStoreAdapter.cs)

---

### Fase 3: Consolidada — Protocolo no AddAIAgent nativo
**Impacto:** Médio | **Esforço:** Médio | **Risco residual:** Médio (testes A2A/AG-UI pendentes)  
**Objetivo:** Remover IChatClient keyed customizado e usar agent nativo direto

**Mudanças:**
1. Registrar `AddAIAgent("AgenticSystem")` como alias do hosted orchestrator via factory delegate
2. Reusar `AgentSessionStore` keyed do orquestrador no path de protocolo
3. Remover o keyed `IChatClient` `protocol-orchestrator` e deletar `ProtocolOrchestratorChatClient`

**Referência de código:**
- [src/AgenticSystem.Api/Program.cs](src/AgenticSystem.Api/Program.cs)
- [src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs](src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs)

---

### Fase 4: Consolidada — Caminho direto sem AgentFrameworkAdapter
**Impacto:** Médio | **Esforço:** Médio | **Risco residual:** Baixo (teste manual do direct path pendente)  
**Objetivo:** Usar ChatClientAgent nativo sem wrapper proprietário de `IAgent`

**Mudanças:**
1. Remover `AgentFrameworkAdapter` e `AgentFrameworkAgentFactory`
2. Substituir `IDirectAgentExecutionFactory` por `IDirectAgentExecutionService`
3. Executar o framework diretamente em `AgentFrameworkDirectExecutionService`, mantendo fallback para o agente cru em erro crítico

**Referência de código:**
- [src/AgenticSystem.Core/Services/DirectAgentRequestExecutor.cs](src/AgenticSystem.Core/Services/DirectAgentRequestExecutor.cs)
- [src/AgenticSystem.Infrastructure/AgentFramework/AgentFrameworkDirectExecutionService.cs](src/AgenticSystem.Infrastructure/AgentFramework/AgentFrameworkDirectExecutionService.cs)
- [src/AgenticSystem.Core/Interfaces/IDirectAgentExecutionService.cs](src/AgenticSystem.Core/Interfaces/IDirectAgentExecutionService.cs)

---

### Fase 5: Enriquecer workflows com recursos nativos do MAF
**Impacto:** Médio | **Esforço:** Alto | **Risco:** Médio (exige novos testes)  
**Objetivo:** Expandir além de BuildSequential para concurrent, handoff e checkpoints

**Mudanças:**
1. Adicionar `BuildConcurrent` para tarefas paralelas (Análise + RAG)
2. Integrar handoff workflow nativo no review colaborativo
3. Integrar termination policies nativas em workflows
4. Adicionar checkpointing para resumir workflows interruptos

**Status do slice atual:**
- ✅ `BuildConcurrent` + contexto compartilhado no `AgentCollaborationWorkflow`
- ✅ Checkpointing com `CheckpointManager.Default` no workflow colaborativo avançado
- ✅ Handoff review com `HandoffWorkflowBuilder`
- ✅ Termination policy com `RoundRobinGroupChatManager` no review colaborativo

**Cobertura atual:**
- ✅ `AgentCollaborationWorkflowTests` valida baseline sequencial
- ✅ `AgentCollaborationWorkflowTests` valida concurrent context + checkpointing
- ✅ `AgentCollaborationWorkflowTests` valida handoff review nativo
- ✅ `AgentCollaborationWorkflowTests` valida group chat termination nativa
- ✅ Suíte completa `AgenticSystem.Tests` passou com 535/535 após os slices de workflows avançados

**Decisão de rollout atual:**
- ✅ Permanecer como experimento controlado no slice colaborativo
- ✅ Flags continuam `false` por padrão em `appsettings`
- ⏳ Promoção para o runtime central depende de stress test, validação manual de observabilidade/streaming e aprovação de product

---

## 3. Ordem de Execução Recomendada

| # | Fase | Duração | Dependência | Reversível? |
|---|------|---------|-------------|------------|
| 1 | Consolidar OrchestratorContextFactory | ~2 dias | Nenhuma | ✅ Sim |
| 2 | Consolidar SimpleSessionStoreAdapter | ~2 dias | Fase 1 | ✅ Sim |
| 3 | Consolidar ProtocolOrchestratorChatClient | ~3 dias | Fase 1-2 | ⚠️ Requer testes A2A |
| 4 | Remover AgentFrameworkAdapter | ~2 dias | Fase 1-2 | ✅ Sim |
| 5 | Workflows avançados | ~5 dias | Fases 1-4 | ✅ Sim |

**Caminho crítico:** Fases 1-2 em paralelo, depois 3-4 em paralelo, por fim 5.  
**Tempo total:** ~2 sprints para fases 1-4, +1 sprint para fase 5 se aprovado.

---

## 4. Validação por Fase

### Fase 1 ✅ Pronto
- [ ] Todos os testes de orquestração passam
- [ ] Agent selection (SmartRouter) funciona
- [ ] RAG injection funciona
- [ ] Quality gates funcionam
- [ ] Teste manual: rota para especialista correto

### Fase 2 ✅ Pronto  
- [ ] Session persistence funciona
- [ ] Session restore funciona
- [ ] Chat history mantido corretamente
- [ ] Teste manual: multi-turn conversation

### Fase 3 ⚠️ Implementada, validação manual pendente
- [x] `AddAIAgent("AgenticSystem")` resolve o hosted orchestrator nativo
- [x] Wrapper `ProtocolOrchestratorChatClient` removido
- [x] Build da API concluído sem erros
- [ ] A2A server responde sem erros
- [ ] AG-UI recebe respostas corretas
- [ ] Teste manual: chamar via A2A endpoint

### Fase 4 ⚠️ Implementada, validação manual pendente
- [x] `AgentFrameworkAdapter` removido
- [x] `AgentFrameworkAgentFactory` removida
- [x] `DirectAgentRequestExecutor` usa `IDirectAgentExecutionService`
- [x] Build da API concluído sem erros
- [x] Testes unitários do direct path passam
- [ ] ExecuteDirectAsync funciona
- [ ] Chat direto para agente nomeado funciona
- [ ] Fallback para agente cru dispara apenas em erro crítico
- [ ] Teste manual: frontend seleciona agent explícito

### Fase 5 ⚠️ Requer aprovação de product
- [x] Workflows paralelos disparam corretamente no slice colaborativo
- [x] Termination policies respeitadas no slice colaborativo
- [x] Checkpointing salva estado corretamente no slice colaborativo
- [x] Suíte unitária ampla do runtime passa sem regressão
- [ ] Teste de stress com workflows longos
- [ ] Validação manual end-to-end com streaming/telemetria do modo avançado
- [ ] Decisão formal de promoção além do slice colaborativo

---

## 5. Arquivos a Modificar / Remover / Criar

### Modificar
- ✏️ `src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextFactory.cs` (Fase 1)
- ✏️ `src/AgenticSystem.Infrastructure/AgentFramework/SimpleSessionStoreAdapter.cs` (Fase 2)
- ✏️ `src/AgenticSystem.Infrastructure/AgentFramework/FrameworkOrchestratorService.cs` (Fases 1-2)
- ✏️ `src/AgenticSystem.Api/Program.cs` (Fase 3)
- ✏️ `src/AgenticSystem.Core/Services/DirectAgentRequestExecutor.cs` (Fase 4)
- ✏️ `src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs` (Fase 4)

### Remover
- 🗑️ `src/AgenticSystem.Infrastructure/AgentFramework/ProtocolOrchestratorChatClient.cs` (Fase 3)
- 🗑️ `src/AgenticSystem.Infrastructure/AgentFramework/AgentFrameworkAdapter.cs` (Fase 4)
- 🗑️ `src/AgenticSystem.Infrastructure/AgentFramework/AgentFrameworkAgentFactory.cs` (Fase 4)
- 🗑️ `src/AgenticSystem.Core/Interfaces/IDirectAgentExecutionFactory.cs` (Fase 4)
- 🗑️ `src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextResolver.cs` (Fase 1)

### Criar
- ➕ `src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorHostBuilder.cs` (Fase 1)
- ➕ `src/AgenticSystem.Infrastructure/AgentFramework/SimpleSessionStoreAdapter.cs` (Fase 2)
- ➕ `src/AgenticSystem.Infrastructure/AgentFramework/AgentFrameworkDirectExecutionService.cs` (Fase 4)
- ➕ `src/AgenticSystem.Core/Interfaces/IDirectAgentExecutionService.cs` (Fase 4)

---

## 6. Backlog Pós-Refatoração

- [ ] Remover `OrchestratorContext` record (mudar apenas para `ChatClientAgent`)
- [ ] Renomear `FrameworkOrchestratorService` para `HostedOrchestratorService` (mais claro)
- [ ] Documentar pattern final de "Custom Governance + Native Hosting"
- [ ] Gerar exemplo de novo agent customizado seguindo padrão nativo
- [ ] Considerar `GroupChatAgent` nativo do MAF para colaboração avançada (v1.1+)

---

## 7. Riscos e Mitigação

| Risco | Severidade | Mitigação |
|-------|-----------|-----------|
| Regressão em orquestração | Alta | Suite de testes > 90% coverage no core |
| Break em A2A/AG-UI | Média | Testes de integração por fase + canary |
| Session loss em Fase 2 | Média | Manter fallback por 1 sprint |
| Conflito com workflows colaborativos | Média | Fase 5 depois de validação de Fases 1-4 |
| Incompatibilidade com MAF version | Baixa | Pinnar versão testada em csproj |

---

## 8. Métricas de Sucesso

- **Linhas de código reduzidas:** -300 LOC (de wrappers transitórios)
- **Componentes 100% nativos:** OrchestratorContextFactory → OrchestratorHostBuilder
- **Session store consolidado:** SimpleSessionStoreAdapter como única implementação do runtime
- **Testes passando:** 100% (baseline)
- **Documentação atualizada:** Registrar novo pattern em docs/

---
