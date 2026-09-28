# Plano de Migração Completa — MAF 1.6.2 (Revisado)

> **Status:** Planejamento  
> **Data:** 25 de Maio de 2026  
> **Princípio Arquitetural:** Tudo é criado dinamicamente pelo chat e persistido no PostgreSQL. Nada deve depender de arquivos em disco ou configuração estática em C#.

---

## Auditoria Completa: O que JÁ está implementado

Após varredura exaustiva de todo o backend, confirmo que o projeto está **muito mais avançado** do que o roadmap antigo (`maf-migration-roadmap.md`) sugere. O upgrade de pacotes para 1.6.2 já foi concluído e diversas fases já foram parcialmente ou totalmente implementadas.

### ✅ Fase 1 — Upgrade de Infraestrutura (1.5.0 → 1.6.2): **COMPLETA**

| Pacote | Versão | Projeto |
|--------|--------|---------|
| `Microsoft.Agents.AI` | 1.6.2 | Core + Infrastructure |
| `Microsoft.Agents.AI.Abstractions` | 1.6.2 | Infrastructure |
| `Microsoft.Agents.AI.Hosting` | 1.6.2-preview | Infrastructure |
| `Microsoft.Agents.AI.Workflows` | 1.6.2 | Infrastructure |
| `Microsoft.Agents.AI.OpenAI` | 1.6.2 | Infrastructure |
| `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` | 1.6.2-preview | Api |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | 1.6.2-preview | Api |
| `Microsoft.Agents.AI.DevUI` | 1.6.2-preview | Api |
| `Microsoft.Extensions.AI.Evaluation` | 10.6.0 | Core + Api |
| `Microsoft.Extensions.AI.Evaluation.Quality` | 10.6.0 | Core + Api |

### ✅ Fase 2 — Skills Framework: **COMPLETA (DB-First)**

O projeto **já implementou** o Skills Framework de forma nativa com persistência em banco:

- [DbAgentSkillsSource](../../src/AgenticSystem.Infrastructure/AgentFramework/DbAgentSkillsSource.cs) — Carrega skills do PostgreSQL com auto-seeding por Tenant.
- [AgentSkillsProvider](../../src/AgenticSystem.Infrastructure/AgentFramework/AgentSkillsProvider.cs) — Herda `MessageAIContextProvider` oficial do MAF, injetando skills dinamicamente no pipeline da LLM.
- [DbBasedSkill](../../src/AgenticSystem.Infrastructure/AgentFramework/DbAgentSkillsSource.cs) — Implementação de `ISkill` baseada em registros do banco.
- [DynamicSkillCatalogHostedService](../../src/AgenticSystem.Infrastructure/Skills/DynamicSkillCatalogHostedService.cs) — Carrega skills declarativas de YAML/JSON em disco como complemento.
- **API CRUD** via `AgentSkillsController` já exposta.

### ✅ Fase 4 — Configuração Declarativa (YAML): **PARCIALMENTE IMPLEMENTADA**

- [AgentYamlValidator](../../src/AgenticSystem.Infrastructure/AgentFramework/AgentYamlValidator.cs) — Parser e validador de manifestos YAML dos agentes com DTOs completos (`AgentYamlDto`, `AgentYamlMetadataDto`, etc.).
- [AgentConfigurationService](../../src/AgenticSystem.Core/Services/AgentConfigurationService.cs) — Persistência de configurações no banco.
- ⚠️ **PowerFx**: Validação sintática básica (parênteses balanceados) existe, mas o motor `RecalcEngine` do `Microsoft.PowerFx` **NÃO está instalado nem integrado**. As expressões são validadas superficialmente.

### ✅ Fase 5 — Sandbox WASM: **PARCIALMENTE IMPLEMENTADA**

- [HyperlightSandboxedExecutor](../../src/AgenticSystem.Infrastructure/Security/HyperlightSandboxedExecutor.cs) — Classe implementada e registrada no DI, exposta como `HyperlightExecuteCodeTool` para os agentes.
- ⚠️ **Porém é uma SIMULAÇÃO**: A execução não usa o SDK real do Hyperlight WASM. O código simula a sandbox com `Task.Delay` e outputs hardcoded. Não há isolamento real de micro-VM.
- ⚠️ **FIDES**: Não implementado. Nenhum middleware de rastreamento de dados sensíveis existe.

### ✅ Fase 6 — DevUI e Evaluators: **IMPLEMENTADA**

- `AddDevUI()` e `MapDevUI()` registrados no [Program.cs](../../src/AgenticSystem.Api/Program.cs) (linhas 98 e 168).
- [RuntimeEvaluatorService](../../src/AgenticSystem.Core/Services/RuntimeEvaluatorService.cs) — Usa `CompositeEvaluator` com `FluencyEvaluator` e `RelevanceTruthAndCompletenessEvaluator` do `Microsoft.Extensions.AI.Evaluation.Quality`.
- [AgentEvaluationService](../../src/AgenticSystem.Core/Services/AgentEvaluationService.cs) — Usa `LocalEvaluator` com `FunctionEvaluator.Create()` para checks customizados (KeywordCoverage, SafetyCheck, HallucinationGuard).
- API REST exposta via `AgentRuntimeController` (endpoints `/evaluate` e `/regressions`).

### ✅ Criação Dinâmica de Agentes: **IMPLEMENTADA**

- [DynamicAgentService](../../src/AgenticSystem.Core/Services/DynamicAgentService.cs) — Cria agentes via linguagem natural usando LLM para gerar `AgentSpecification`.
- [HierarchicalAgentFactory](../../src/AgenticSystem.Core/Services/HierarchicalAgentFactory.cs) — Pool de agentes com suporte a `CustomAgent` dinâmico.
- ⚠️ **Problema**: Agentes dinâmicos vivem apenas **in-memory** (`ConcurrentDictionary`). Se o servidor reiniciar, eles somem. Não há persistência de `AgentSpecification` no PostgreSQL.

---

## O que FALTA implementar

Baseado na auditoria acima e no princípio de "tudo dinâmico + banco", restam os seguintes gaps:

### Gap 1: Persistência de Agentes Dinâmicos no Banco (CRÍTICO)

> [!CAUTION]
> Agentes criados via chat (`DynamicAgentService`) são perdidos ao reiniciar o servidor. Isso contradiz o princípio de "criar tudo e salvar no banco".

#### [NEW] `src/AgenticSystem.Infrastructure/Persistence/Entities/DynamicAgentEntity.cs`
- Entidade EF Core com campos: `Name`, `Description`, `Domain`, `Tier`, `Instructions`, `AllowedTools` (JSON), `Configuration` (JSON), `TenantId`, `IsActive`, `CreatedAt`.

#### [MODIFY] `src/AgenticSystem.Infrastructure/Persistence/ApplicationDbContext.cs`
- Adicionar `DbSet<DynamicAgentEntity>`.

#### [MODIFY] `src/AgenticSystem.Core/Services/HierarchicalAgentFactory.cs`
- No `InitializeDefaultAgents()`, carregar também os agentes dinâmicos do banco.
- No `CreateCustomAgentAsync()`, persistir a `AgentSpecification` no PostgreSQL.

#### [NEW] EF Core Migration
```bash
dotnet ef migrations add AddDynamicAgentsTable --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --output-dir Persistence/Migrations
```

---

### Gap 2: Sessões Duráveis (`DurableTask`) — FUTURO

> [!IMPORTANT]
> O pacote `Microsoft.Agents.AI.DurableTask` **não está instalado**. A persistência de sessão continua sendo customizada via `SimpleSessionStoreAdapter` → PostgreSQL.

#### Decisão Necessária do Usuário:
O `SimpleSessionStoreAdapter` atual funciona bem para o caso de uso do projeto? Ou precisa de resiliência nativa (retry automático, checkpointing de workflows longos, Human-in-the-Loop durável)?

- **Se o atual é suficiente**: Manter `SimpleSessionStoreAdapter` como está. Documentar como decisão arquitetural.
- **Se precisa de DurableTask**: Adicionar o pacote, criar as tabelas de orquestração no PostgreSQL, e migrar os workflows de `InProcessExecution` para execução durável com `InstanceId = "{TenantId}:{SessionId}"`.

---

### Gap 3: PowerFx Real (Motor de Expressões)

> [!WARNING]
> O `AgentYamlValidator` valida expressões PowerFx apenas com checagem de parênteses. O motor real (`RecalcEngine`) não está instalado.

#### [MODIFY] `src/AgenticSystem.Infrastructure/AgenticSystem.Infrastructure.csproj`
- Adicionar `Microsoft.PowerFx.Core` e `Microsoft.PowerFx.Interpreter`.

#### [MODIFY] `src/AgenticSystem.Infrastructure/AgentFramework/AgentYamlValidator.cs`
- Substituir a validação de parênteses pela compilação real via `RecalcEngine` com:
  - Funções permitidas: `If`, `And`, `Or`, `Sum`, `Abs`, `Concat`, `Len`, `Upper`, `Lower`.
  - Timeout de 50ms via `CancellationToken`.
  - Contexto higienizado com `TenantContextRecord`.

---

### Gap 4: Hyperlight WASM Real (Sandbox de Código)

#### Situação Atual:
O `HyperlightSandboxedExecutor` é um **stub** que simula execução. Não há isolamento real.

#### Decisão Necessária:
- O SDK `Hyperlight` da Microsoft está disponível como pacote NuGet público? Se sim, substituir a simulação pela execução real em micro-VM/WASM.
- Se não está disponível publicamente, documentar como "Lab Feature" e manter a simulação com warning nos logs.

---

### Gap 5: FIDES Middleware (Segurança de Dados Sensíveis)

#### Situação: Não implementado.

#### [NEW] `src/AgenticSystem.Infrastructure/Security/FidesDataProtectionMiddleware.cs`
- Middleware no pipeline do `AIAgentBuilder` que intercepta mensagens antes de enviar ao LLM.
- Escaneia e mascara dados sensíveis (CPF, cartão de crédito, tokens) usando regex patterns configuráveis por Tenant no banco.
- Registrar via `builder.Use(inner => new FidesDataProtectionMiddleware(inner, ...))`.

---

## Priorização Recomendada

| Prioridade | Gap | Justificativa |
|:---:|:---|:---|
| **P0** | Gap 1 — Persistir agentes dinâmicos no banco | Contradiz o princípio central do projeto. Dados somem no restart. |
| **P1** | Gap 3 — PowerFx real | Validação de YAML é superficial. Risco de expressões inválidas em produção. |
| **P2** | Gap 5 — FIDES | Segurança de dados do Tenant. Compliance. |
| **P3** | Gap 2 — DurableTask | Depende de necessidade real de resiliência. Pode esperar. |
| **P4** | Gap 4 — Hyperlight real | Depende de disponibilidade do SDK. Lab feature. |

---

## Documentação a Atualizar

Após execução:
1. Atualizar [backend-architecture-explained.md](../architecture/backend-architecture-explained.md) — Seções 4, 12, 13.
2. Atualizar [maf-migration-roadmap.md](maf-migration-roadmap.md) — Marcar fases concluídas.
3. Criar ADR para decisão sobre DurableTask (manter vs migrar).

## Verification Plan

### Automated Tests
- `dotnet test` — Garantir 608 testes passando.
- Novo teste de integração: criar agente dinâmico → reiniciar contexto → verificar que o agente persiste no banco.
- Teste de PowerFx: submeter expressão inválida no YAML e garantir rejeição.

### Manual Verification
- Criar agente via chat, reiniciar o servidor, verificar que o agente aparece na listagem.
- Acessar DevUI (`/devui`) e debugar um fluxo de chat.
