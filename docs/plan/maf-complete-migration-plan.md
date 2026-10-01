# Plano de Migração Completa — MAF 1.6.2 (Revisado)

> **Decisões de produto (registradas em 2026-09-29; estado atualizado em 2026-10-01):** #104 valida sintaxe PowerFx apenas; #105 Hyperlight Preview está integrado com flag global off e habilitação somente em Lab; #106 FIDES usa detectores built-in, políticas/toggles por tenant, OCR e fail-closed; DurableTask não agenda grafos dinâmicos. Ver [especificações das issues](open-issues-specification-audit-2026-09-29.md), [ADR-036](../architecture/adr/036-maf-122-protocols-and-gateway.md), [ADR-037](../architecture/adr/037-a2a-agui-preview-validation.md), [ADR-040](../architecture/adr/040-self-improvement-human-approval.md) e os planos correntes.

> **Status:** Histórico / supersedido pela linha MAF 1.22
>
> **Estado atual:** fotografia histórica da linha MAF 1.6.x, supersedida pela atualização para MAF 1.22 registrada em [#120](https://github.com/JonathanBenicio/Agent-System/issues/120) e [ADR-036](../architecture/adr/036-maf-122-protocols-and-gateway.md). Os itens #104–#106 abaixo são referências de decisões, não aceite para implementação pendente nesta versão.
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

### Gap 2: DurableTask para workflows dinâmicos — decisão registrada

Em 2026-09-29 ficou decidido não usar DurableTask como scheduler de grafos arbitrários por tenant. O engine `IWorkflowEngine`/`IWorkflowStore` da aplicação agenda essas definições versionadas; MAF executa agentes. Sessões MAF permanecem no store PostgreSQL do produto. DurableTask pode ser reconsiderado somente com suporte comprovado a registry dinâmico, isolamento, recuperação e versão de workflow. Ver ADR-036/038 e issues #120/#122.

---

### Gap 3: PowerFx — validar sintaxe, sem execução em runtime

Decisão de produto: manter `RecalcEngine.Check` no `AgentYamlValidator` para rejeitar fórmulas inválidas. Não executar expressões PowerFx em runtime até que um caso de uso seja aprovado por nova issue. A lista de funções e o contexto seguro do plano histórico não habilitam avaliação.

---

### Gap 4: Hyperlight WASM — Preview sob flag global

Decisão de produto: integrar `Microsoft.Agents.AI.Hyperlight` Preview atrás de flag global desligada por padrão, habilitável somente em Lab e após testes de segurança. O executor atual continua simulado; flag desligada ou pacote ausente deve retornar capacidade indisponível, sem output hardcoded que pareça execução. Filesystem e rede ficam negados por padrão. Threat model, timeout, memória, cancelamento, isolamento e compatibilidade devem passar antes de habilitar. Ver ADR-006 e issue #105.

---

### Gap 5: FIDES — regras built-in, políticas tenant e OCR

Decisão de produto: detectores built-in revisados, toggles de política por tenant gerenciados por Owner/Admin; todos ativos por padrão e detectores obrigatórios de credenciais não podem ser desligados. Não aceitar regex arbitrária de tenant. Incluir scan/OCR de imagens e anexos antes do provider. Se detectar conteúdo sensível que não possa ser redigido com confiança, bloquear a chamada e pedir mídia redigida. Falha/timeout do detector obrigatório falha fechada; não registrar o dado original. Formatos suportados e implementação técnica serão definidos no plano da issue #106.

---
## Priorização histórica — decisões vigentes em 2026-09-29

| Prioridade | Gap | Justificativa |
|:---:|:---|:---|
| **P0** | Gap 1 — Persistir agentes dinâmicos no banco | Contradiz o princípio central do projeto. Dados somem no restart. |
| **P1** | Gap 3 — PowerFx runtime | Adiado por decisão: sintaxe somente até caso de uso aprovado. |
| **P2** | Gap 5 — FIDES + OCR | Política decidida; implementação de toggles/OCR e segurança fail-closed pendentes. |
| **P3** | Gap 2 — DurableTask | Não usar como scheduler de grafos dinâmicos por tenant; decisão documentada em ADR-036/038. |
| **P4** | Gap 4 — Hyperlight Preview | Integrar somente atrás de flag global off por padrão, em Lab, após testes de segurança. |

---

## Documentação a Atualizar

Após execução:
1. Atualizar [backend-architecture-explained.md](../architecture/backend-architecture-explained.md) — Seções 4, 12, 13.
2. Atualizar [maf-migration-roadmap.md](maf-migration-roadmap.md) — Marcar fases concluídas.
3. DurableTask dinâmico: decisão registrada em ADR-036/038; não migrar até existir requisito e compatibilidade comprovados.

## Verification Plan

### Automated Tests
- `dotnet test` — Garantir 608 testes passando.
- Novo teste de integração: criar agente dinâmico → reiniciar contexto → verificar que o agente persiste no banco.
- Teste de PowerFx: submeter expressão inválida no YAML e garantir rejeição.

### Manual Verification
- Criar agente via chat, reiniciar o servidor, verificar que o agente aparece na listagem.
- Acessar DevUI (`/devui`) e debugar um fluxo de chat.
