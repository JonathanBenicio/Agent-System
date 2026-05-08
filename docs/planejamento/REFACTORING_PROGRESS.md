# Progresso de Refatoração — MAF Native Runtime

> **[TRANSITIONAL STATUS BOARD]** Este arquivo consolida progresso e backlog residual da refatoração MAF nativa.
> As fases concluídas devem ser lidas como registro histórico; a arquitetura operacional vigente continua em [../architecture/backend-architecture-explained.md](../architecture/backend-architecture-explained.md).

**Data da última atualização:** 7 maio 2026  
**Status global:** Fases 1-4 Completas ✅ | Fase 5 Em andamento ⏳

---

## Sumário Executivo

| Métrica | Antes | Depois | Redução |
|---------|-------|--------|---------|
| **Composição (LOC)** | 240 | 60 | -75% |
| **Sessão (LOC)** | 220 | 110 | -50% |
| **Protocolo (wrapper)** | 90 | 0 | -100% |
| **Direct path wrapper** | 50 | 0 | -100% |
| **Total rastreado (LOC)** | ~600 | ~120 | -80% |
| **Dívida transitória** | 8 camadas | 4 camadas | -4 componentes |

---

## Roadmap Executado

### ✅ Fase 1: Consolidar Composição em OrchestratorHostBuilder

**Objetivo:** Mover lógica manual de composição para builder native MAF  
**Status:** ✅ COMPLETA  
**Documentação:** [REFACTORING_CHECKPOINT_PHASE1.md](./REFACTORING_CHECKPOINT_PHASE1.md)

**Arquivos Criados:**
- ✅ `OrchestratorHostBuilder.cs` (~130 linhas) — Builder nativo encapsulado

**Arquivos Refatorados:**
- ✅ `OrchestratorContextFactory.cs` (-180 LOC) — Thin wrapper apenas
- ✅ `ServiceCollectionExtensions.cs` — Registro atualizado

**Compilação:** ✅ 0 erros

**Próximo passo:** Validação (unit tests, SmartRouter, RAG, QualityGates)

---

### ✅ Fase 2: Simplificar Sessão — Remover Fallbacks Legados

**Objetivo:** Eliminar 3 estratégias de key resolution, manter apenas 1 (agent name)  
**Status:** ✅ COMPLETA  
**Documentação:** [REFACTORING_CHECKPOINT_PHASE2.md](./REFACTORING_CHECKPOINT_PHASE2.md)

**Arquivos Criados:**
- ✅ `SimpleSessionStoreAdapter.cs` (~110 linhas) — Adapter sem fallbacks

**Arquivos Refatorados:**
- ✅ Runtime migrado para `SimpleSessionStoreAdapter`
- ✅ Adapter legado removido após a migração final

**Compilação:** ✅ 0 erros

**Validação adicional:**
- ✅ `dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --filter "FullyQualifiedName~DirectAgentRequestExecutorTests|FullyQualifiedName~AgentFrameworkDirectExecutionServiceTests|FullyQualifiedName~SimpleSessionStoreAdapterTests"`
- ✅ Warnings de obsolescência do session store removidos do build

**Próximo passo:** Testes de persistência e multi-turn conversations no runtime real

---

### ✅ Fase 3: Consolidar Protocolo — Remover ProtocolOrchestratorChatClient

**Objetivo:** Eliminar IChatClient keyed wrapper, usar agent nativo direto  
**Status:** ✅ COMPLETA  
**Documentação:** [REFACTORING_CHECKPOINT_PHASE3.md](./REFACTORING_CHECKPOINT_PHASE3.md)

**Arquivos Refatorados:**
- ✅ `Program.cs` — `AddAIAgent("AgenticSystem")` agora aponta direto para o orquestrador hospedado
- ✅ `ServiceCollectionExtensions.cs` — Removido keyed `IChatClient` de protocolo

**Arquivos Removidos:**
- ✅ `ProtocolOrchestratorChatClient.cs` — Wrapper de protocolo eliminado

**Validação:**
- ✅ `get_errors` limpo nos arquivos alterados
- ✅ `dotnet build src/AgenticSystem.Api/AgenticSystem.Api.csproj`
- ⏳ Testes manuais A2A/AG-UI ainda pendentes

**Benefício:** -90 LOC, -1 wrapper layer

---

### ✅ Fase 4: Remover AgentFrameworkAdapter

**Objetivo:** Eliminar adapter wrapper em ExecuteDirectAsync  
**Status:** ✅ COMPLETA  
**Documentação:** [REFACTORING_CHECKPOINT_PHASE4.md](./REFACTORING_CHECKPOINT_PHASE4.md)

**Arquivos Criados:**
- ✅ `AgentFrameworkDirectExecutionService.cs` — Execução direta no framework sem wrapper de `IAgent`
- ✅ `IDirectAgentExecutionService.cs` — Novo contrato de execução direta

**Arquivos Refatorados:**
- ✅ `DirectAgentRequestExecutor.cs` — Passou a depender de serviço de execução direta
- ✅ `ServiceCollectionExtensions.cs` — Registro DI atualizado para o novo serviço

**Arquivos Removidos:**
- ✅ `AgentFrameworkAdapter.cs` — Wrapper eliminado
- ✅ `AgentFrameworkAgentFactory.cs` — Factory transitória eliminada
- ✅ `IDirectAgentExecutionFactory.cs` — Contrato antigo removido

**Validação:**
- ✅ `get_errors` limpo nos arquivos alterados
- ✅ `dotnet build src/AgenticSystem.Api/AgenticSystem.Api.csproj`
- ✅ `dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --filter "FullyQualifiedName~DirectAgentRequestExecutorTests|FullyQualifiedName~AgentFrameworkDirectExecutionServiceTests"`
- ✅ Migração do session store eliminou os warnings de obsolescência do runtime

**Benefício:** -1 adapter layer no caminho direto, contrato simplificado

---

### ⏳ Fase 5: Enriquecer Workflows com MAF Nativo

**Objetivo:** Adicionar BuildConcurrent, termination policies, checkpointing  
**Status:** ⚠️ EM ANDAMENTO  
**Esforço:** ~2-3 dias | **Risco:** Médio  
**Requer:** Aprovação de produto

**Recursos a explorar:**
- `AgentWorkflowBuilder.BuildConcurrent()` — Parallel agent execution
- Termination policies — Stop conditions para workflows
- Checkpointing — State save entre execuções
- Loop detection — Evitar infinitas colaborações

**Slice já implementado:**
- ✅ `BuildConcurrent` para contexto compartilhado de RAG/canal no `AgentCollaborationWorkflow`
- ✅ Checkpointing com `CheckpointManager.Default` no workflow colaborativo avançado
- ✅ Handoff workflow nativo no review colaborativo (`HandoffWorkflowBuilder`)
- ✅ Termination policy nativa no review colaborativo com `RoundRobinGroupChatManager`

**Validação atual do slice:**
- ✅ `dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --filter "FullyQualifiedName~AgentCollaborationWorkflowTests"`
- ✅ 4 testes verdes cobrindo sequential baseline, concurrent context, handoff review e group chat termination
- ✅ `dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj`
- ✅ 535 testes verdes na suíte completa, sem regressão fora do workflow colaborativo

**Decisão atual:**
- ✅ Manter o modo avançado isolado no `AgentCollaborationWorkflow` e atrás de flags desligadas por padrão
- ⏳ Promover para caminhos mais centrais apenas após stress test, validação manual end-to-end e definição explícita de rollout/product

**Benefício:** Habilitá workflows mais ricos (não apenas Sequential)

---

## Checkpoint por Arquivo

### Composição

| Arquivo | Antes | Depois | Status |
|---------|-------|--------|--------|
| OrchestratorContextFactory.cs | 240 LOC | 60 LOC | ✅ Refatorado |
| OrchestratorHostBuilder.cs | N/A | 130 LOC | ✅ Criado |
| ServiceCollectionExtensions.cs | ? | Atualizado | ✅ Refatorado |

### Sessão

| Arquivo | Antes | Depois | Status |
|---------|-------|--------|--------|
| AgentFrameworkSessionStoreAdapter.cs | 220 LOC | Removido | ✅ Migrado |
| SimpleSessionStoreAdapter.cs | N/A | 110 LOC | ✅ Runtime final |

### Protocolo

| Arquivo | Antes | Depois | Status |
|---------|-------|--------|--------|
| ProtocolOrchestratorChatClient.cs | ~90 LOC | Removido | ✅ Concluído |
| Program.cs | keyed IChatClient | Alias nativo do hosted orchestrator | ✅ Concluído |

### Adapters

| Arquivo | Antes | Depois | Status |
|---------|-------|--------|--------|
| AgentFrameworkAdapter.cs | ~50 LOC | Removido | ✅ Concluído |
| AgentFrameworkAgentFactory.cs | wrapper factory | Removido | ✅ Concluído |
| DirectAgentRequestExecutor.cs | wrapper factory opcional | Serviço direto opcional | ✅ Concluído |
| AgentFrameworkDirectExecutionService.cs | N/A | Novo | ✅ Concluído |

---

## Dívida Técnica Impactada

### Resolvida ✅

- ❌ ➜ ✅ `OrchestratorContextFactory` — Agora thin wrapper, composição em builder nativo
- ❌ ➜ ✅ Fallbacks de session key (3 estratégias) — Agora 1 estratégia (agent name)
- ❌ ➜ ✅ `ProtocolOrchestratorChatClient` — Protocolo usa `AddAIAgent` nativo apontando para o hosted orchestrator
- ❌ ➜ ✅ `AgentFrameworkAdapter` — Caminho direto executa framework sem wrapper transitório de `IAgent`

### Pending (Próximas Fases) ⏳

- ⏳ `OrchestratorContextFactory` + `OrchestratorHostBuilder` — Review pendente da composição hosted remanescente

### Mantida por Design ✅

(Permanece como diferencial de produto)

- ✅ `IAgentExecutionPreProcessingPipeline` — Validação + correction rules
- ✅ `IAgentExecutionPostProcessingPipeline` — Reflection + approval + memory
- ✅ `OrchestratorAuxiliaryTools` — SmartRouter + ContextAnalyzer
- ✅ `GovernedChatClient` — Middleware de governança
- ✅ `FrameworkAgentChannelService` — Collaboração estruturada

---

## Próximos Passos

### Imediato (Esta semana)
1. Validar Fase 1 — executar unit tests
2. Validar Fase 2 — testar persistência e restore
3. Validar Fase 3 — testar A2A e AG-UI no runtime real
4. Validar Fase 4 — testar execução direta por agente nomeado

### Médio (Próximas 2 semanas)
1. Iniciar Fase 5 (workflows)
2. Reduzir warnings de obsolescência do session adapter
3. Consolidar validação manual A2A, AG-UI e direct path

### Longo (Após Fase 4)
1. Refine Fase 5 com stakeholders
2. Implement Fase 5 (workflows + policies)
3. Full test suite + documentation

---

## Como Contribuir

Cada fase é self-contained:
1. Ler checkpoint correspondente (`REFACTORING_CHECKPOINT_PHASE[N].md`)
2. Revisar código em `src/AgenticSystem.Infrastructure/AgentFramework/`
3. Executar testes de validação
4. Abrir PR com descobertas/melhorias

**Crítico:** Não pular fases. Cada fase valida antes de prosseguir.

---

*Última atualização: 7 maio 2026*
