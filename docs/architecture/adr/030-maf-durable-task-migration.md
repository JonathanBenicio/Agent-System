# ADR 030: Arquitetura de Orquestração Nativa — Docker/PostgreSQL

**Status:** Revisado e Implementado\
**Data Original:** 26 de Maio de 2026\
**Data de Revisão:** 26 de Maio de 2026\
**Autor(es):** Antigravity / Jonathan Benicio  
**Issue Relacionada:** #108

---

## Contexto

O Agentic System foi inicialmente planejado para usar `Microsoft.Agents.AI.DurableTask` com Durable Entities para persistência de sessões de agentes. Durante a implementação, descobriu-se que o registro anterior de `DurableTaskClient` não estava disponível no DI do host ASP.NET Core. A integração fora de Azure Functions exige configuração explícita de cliente, serviço de orquestração e worker; não basta trocar o store de sessões.

O ambiente de execução é Docker + PostgreSQL, sem Azure Functions.

---

## Decisão Revisada

Adotar a **arquitetura de orquestração nativa** composta por:

1. **`DefaultWorkflowEngine`** como orquestrador central — gerencia o ciclo de vida de execuções, suporte a paralelismo (Fan-out/Fan-in), chaining sequencial e compensações.
2. **`IWorkflowStore` / PostgreSQL** como backend de persistência de estado — todas as execuções e steps são salvos no banco via EF Core.
3. **`SimpleSessionStoreAdapter` + `ISessionStore`** como store de sessões de agentes — persiste contexto de conversa dos agentes MAF no PostgreSQL.
4. **Padrão Async HTTP API** — `POST /api/workflow/executions/start/{id}` retorna `202 Accepted` com `RunId` para polling via `GET /api/workflow/executions/{id}`.

---

## Padrões Implementados

### 1. Async HTTP API (Anti-Timeout para tarefas longas)

```
POST /api/workflow/executions/start/{id}
→ 202 Accepted + { executionId, statusUrl }

# Loop de polling (cliente)
GET /api/workflow/executions/{executionId}
→ { status: "Running" | "Completed" | "Failed", stepExecutions: [...] }
```

O `StartAsync` do `DefaultWorkflowEngine` usa `Task.Run()` (fire-and-forget) para processar o workflow em background, retornando imediatamente com o estado inicial.

### 2. Function Chaining (Sequencial)

`WorkflowStep.DependsOn` define dependências entre steps. O `DefaultWorkflowEngine` avança apenas quando os steps predecessores estão com status `Completed`.

```json
// WorkflowDefinition.Steps:
[
  { "id": "research", "dependsOn": [] },
  { "id": "summarize", "dependsOn": ["research"] },
  { "id": "review",   "dependsOn": ["summarize"] }
]
```

### 3. Fan-out / Fan-in (Paralelo)

Steps sem dependências entre si são executados em paralelo via `Task.WhenAll()`:

```csharp
// DefaultWorkflowEngine.ProcessExecutionAsync
var tasks = readySteps.Select(step => ExecuteStepAsync(tenantId, execution, step)).ToList();
await Task.WhenAll(tasks); // Fan-out + Fan-in automático
```

Steps do tipo `Parallel` com `ParallelSteps` também executam em paralelo.

### 4. Timeout por Step

Cada step de `Action` tem timeout de 5 minutos por padrão (configurável via `WorkflowStep.Timeout`). Implementado com `CancellationTokenSource` por step.

---

## Separação de Responsabilidades

| Componente | Responsabilidade |
|---|---|
| `DefaultWorkflowEngine` | Fluxo, ordem, paralelismo, compensações |
| `IWorkflowStore` (PostgreSQL) | Persistência de execuções e state checkpoints |
| `IDirectAgentRequestExecutor` | Invocação do agente/LLM em cada step de Action |
| `SimpleSessionStoreAdapter` | Contexto de conversa dos agentes MAF entre steps |
| `WorkflowController` | Async HTTP API (202 + polling) |

---

## O que foi descartado e por quê

| Componente | Razão do descarte |
|---|---|
| `DurableSessionStoreAdapter` | Dependia de cliente de Durable Entities não configurado no host. Deletado. |
| `Microsoft.Agents.AI.DurableTask` como host de sessions | Não adotado para persistência das sessões de chat neste host. |
| Durable Entities para sessão | Não configuradas nem validadas neste host. |

---

## Consequências

### Positivas
- ✅ **Funciona 100% em Docker sem Azure** — sem dependência de cloud vendor.
- ✅ **Resiliência via PostgreSQL** — execuções persistidas sobrevivem a restarts.
- ✅ **Fan-out/Fan-in nativo** — `Task.WhenAll()` com state merge no banco.
- ✅ **Timeout configurável por step** — previne travamento por LLM lento.
- ✅ **Multi-tenant** — `TenantId` em todas as entidades de execução.

### Limitações conhecidas
- ❌ **Sem replay automático** — se o container reiniciar durante um step Action em execução, o step ficará como `Running` no banco. Implementar job de recuperação (reconectar steps "Running" órfãos ao iniciar) é uma evolução futura.
- ❌ **Fan-out não é distribuído** — paralelismo ocorre dentro do mesmo processo. Para paralelismo distribuído entre pods, seria necessário um message broker (RabbitMQ/Redis Streams).

## Integração durável remanescente

`DurableWorkflowCompiler` e o cliente PostgreSQL continuam registrados para workflows dinâmicos. A remoção do adapter de sessões não remove esse caminho. O polling HTTP consultando `IWorkflowStore` ainda precisa ser integrado ao estado do DurableTask; a execução e a retomada não foram validadas com banco real nesta revisão.
