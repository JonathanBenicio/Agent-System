# Plano — Correção dos 32 achados do PR #132
Status: em execução
Prioridade: P1 — regressões verificadas em autorização, isolamento e contratos do núcleo impedem recomendar o merge; subtarefas opcionais/documentais têm prioridade própria.
Issue: [epic #139](https://github.com/JonathanBenicio/Agent-System/issues/139) · ADR: [041](../architecture/adr/041-pr132-review-remediation.md) · Stories: [BACK-REVIEW-139](../USER-STORIES.md#back-review-139--corrigir-os-32-achados-do-pr-132)
Baseline: `e8e813b1887e6e23f4cdf61031392f27e2a32334`, checkout limpo antes da execução, 2026-10-02.
Branch: `codex/pr132-review-remediation`; base/PR alvo: `integration/develop-pr-stack-2026-09-30` (#132), por autorização explícita do usuário. Mesma pasta; sem worktree.

## Objetivo e escopo
Corrigir e verificar R01–R32, com issue por contexto e commits com título/corpo em PT-BR, pontos específicos e `Refs #ID`. O acréscimo solicitado no chat “Revisar documentação do projeto” foi incorporado em `169c3f8` e resolve R30; não presumir que isso conclui #150.

Banco será criado do zero. Validar PostgreSQL 16 do Compose `tests/backend-validation/compose.yml` em 55432, DB temporário exclusivo. Nunca tocar 5432, outro projeto/volume ou migration history de outro banco. DurableTask permanece opcional.

Fora do escopo: MAF 1.23, A2A/AGUI preview (#121), Gateway produção/reload (#133), retomada supervisor multihost (#134), forecast (#135), PR31/73 e migração de bancos legados. Evidência anterior de e8e813b não prova esta correção.

## Etapas
| Etapa | Dependência | Verificação | Estado |
|---|---|---|---|
| 1. Epic/subissues, ADR/stories/plano e índices | baseline e autorização | 32 IDs únicos, 12 subtarefas nativas, links reais | em execução |
| 2. Segurança e isolamento (#140–#143) | plano registrado | auth revogada, FIDES direto, XSS e limites ONNX; ACL/RAG/tenant A/B | pendente |
| 3. Quotas, workflows e serviços (#144–#148) | segurança/contextos | cancelamento, teto uniforme, paralelismo, approvals e round-trip | pendente |
| 4. CI, docs e SQL opcional (#149–#151) | contratos corrigidos | servidores E2E, mocks efetivos, IDs/regra, SQL lotes 0/1/2+ | pendente |
| 5. Gate integrado e PR | commits por contexto | build/testes/lint/E2E, banco vazio/schema, links, diff e estado remoto | pendente |

### Subtarefas por contexto
| Issue/contexto | Achados | Story | Estado |
|---|---|---|---|
| #140 auth | R02, R03, R20, R21 | BACK-REVIEW-139-01 | implementado e validado por contexto; gate integrado pendente |
| #141 fides | R01 | BACK-REVIEW-139-02 | implementado;29 regressões/3skips; gate integrado pendente |
| #142 onnx | R04 | BACK-REVIEW-139-03 | implementado;49 regressões ONNX; gate integrado pendente |
| #143 tenancy-rag | R06, R07, R19, R23 | BACK-REVIEW-139-04 | implementado;65 regressões/1skip + lint/build; PG/browser upload pendentes |
| #144 quotas | R08, R09, R10, R11 | BACK-REVIEW-139-05 | em execução;28 regressões + reserva PG real passaram; gate HTTP/integrado pendente |
| #145 workflows | R12, R13, R14, R15, R16, R25 | BACK-REVIEW-139-06 | implementado parcialmente;34regressões backend + lint/build; browser/PG/gate pendentes |
| #146 analytics | R17 | BACK-REVIEW-139-07 | implementado; DI scoped real/EF InMemory; gate PG pendente |
| #147 memory | R18 | BACK-REVIEW-139-08 | implementado;cache por consulta/filtros/geração; regressões passaram |
| #148 agent-contracts | R22, R24 | BACK-REVIEW-139-09 | pendente |
| #149 ci | R05, R26, R27, R28, R31 | BACK-REVIEW-139-10 | pendente |
| #150 docs | R29, R30 | BACK-REVIEW-139-11 | implementados; IDs/refs e PT-BR/issue por commit; reconciliação final pendente |
| #151 durabletask | R32 | BACK-REVIEW-139-12 | implementado;2PGreais/19migrations/69tabelascolunas/concurrency; gatefinal pendente |

### Matriz integral dos achados
IDs seguem a ordem do relatório final publicado nesta conversa. Um achado só muda para validado quando há evidência que cobre seu cenário.
| ID | Problema | Local/fluxo | Issue | Estado/evidência |
|---|---|---|---|---|
| R01 | FIDES na execução direta | AgentFrameworkDirectExecutionService | [#141](https://github.com/JonathanBenicio/Agent-System/issues/141) | protegido na factory/IChatClient; provider fake direto/stream/tool; gate integrado pendente |
| R02 | Authorization cru sem membership | OpenAIChatCompletionController | [#140](https://github.com/JonathanBenicio/Agent-System/issues/140) | 23 regressões backend + 4 Chromium/lint/build; gate integrado pendente |
| R03 | Stored XSS no preview de skills | SkillsPage | [#140](https://github.com/JonathanBenicio/Agent-System/issues/140) | 23 regressões backend + 4 Chromium/lint/build; gate integrado pendente |
| R04 | Dimensões ONNX sem orçamento | OnnxModelController / DynamicOnnxProcessorTool | [#142](https://github.com/JonathanBenicio/Agent-System/issues/142) | orçamento64MiB/dim4096 com overflow seguro e validação antes de alocar;49 regressões |
| R05 | CI E2E sem servidor | ci.yml / Playwright | [#149](https://github.com/JonathanBenicio/Agent-System/issues/149) | pendente |
$165 regressões + lint/build por contexto; PG/browser upload pendentes |
$165 regressões + lint/build por contexto; PG/browser upload pendentes |
| R08 | Quota diária InMemory sem reset | InMemoryTenantQuotaRepository | [#144](https://github.com/JonathanBenicio/Agent-System/issues/144) | pendente |
| R09 | Consumo de stream interrompido perdido | TenantQuotaChatClient | [#144](https://github.com/JonathanBenicio/Agent-System/issues/144) | pendente |
| R10 | Contagem de sessões truncada antes de filtrar | TenantIsolationService | [#144](https://github.com/JonathanBenicio/Agent-System/issues/144) | pendente |
| R11 | Teto de sessões desigual por caminho | MetaAgentOrchestrator / SessionManager | [#144](https://github.com/JonathanBenicio/Agent-System/issues/144) | pendente |
| R12 | Enum de workflow incompatível com cliente | WorkflowModels / useWorkflowStore | [#145](https://github.com/JonathanBenicio/Agent-System/issues/145) | pendente |
| R13 | Duas aprovações pendentes falham | DefaultWorkflowEngine / WorkflowController | [#145](https://github.com/JonathanBenicio/Agent-System/issues/145) | pendente |
| R14 | Dictionary de outputs paralelo sem sincronização | DefaultWorkflowEngine | [#145](https://github.com/JonathanBenicio/Agent-System/issues/145) | pendente |
| R15 | Grafo inválido é salvo | WorkflowController / WorkflowGraphValidator | [#145](https://github.com/JonathanBenicio/Agent-System/issues/145) | pendente |
| R16 | Claim global sem capability de sistema | WorkflowExecutionBackgroundService / PostgresWorkflowStore | [#145](https://github.com/JonathanBenicio/Agent-System/issues/145) | pendente |
| R17 | Analytics consulta contexto descartado | TenantAnalyticsTool | [#146](https://github.com/JonathanBenicio/Agent-System/issues/146) | scope vivo até consulta;4 comandos/A-B validados |
| R18 | Cache de memória não inclui consulta | MemoryInjectionService | [#147](https://github.com/JonathanBenicio/Agent-System/issues/147) | pergunta/maxMemories/tenant/usuário particionados; vectorization invalida |
$165 regressões + lint/build por contexto; PG/browser upload pendentes |
| R20 | Logout não limpa cookie backend | authStore | [#140](https://github.com/JonathanBenicio/Agent-System/issues/140) | 23 regressões backend + 4 Chromium/lint/build; gate integrado pendente |
| R21 | API key persistida no localStorage | authStore | [#140](https://github.com/JonathanBenicio/Agent-System/issues/140) | 23 regressões backend + 4 Chromium/lint/build; gate integrado pendente |
| R22 | Rotas de tools incompatíveis | AgentToolsController | [#148](https://github.com/JonathanBenicio/Agent-System/issues/148) | pendente |
$165 regressões + lint/build por contexto; PG/browser upload pendentes |
| R24 | YAML de UI/template incompatível | AgentFormModal / agent-manifest-template.yaml | [#148](https://github.com/JonathanBenicio/Agent-System/issues/148) | pendente |
| R25 | UI aprova apesar de falha HTTP | useWorkflowExecution | [#145](https://github.com/JonathanBenicio/Agent-System/issues/145) | pendente |
| R26 | Teste XSS não renderiza payload | chat-security.e2e.spec.ts | [#149](https://github.com/JonathanBenicio/Agent-System/issues/149) | pendente |
| R27 | Timeout E2E divergente | chat-timeout.e2e.spec.ts | [#149](https://github.com/JonathanBenicio/Agent-System/issues/149) | pendente |
| R28 | Diagnósticos usam Compose de outro projeto | core/session-diagnostics.mjs | [#149](https://github.com/JonathanBenicio/Agent-System/issues/149) | pendente |
| R29 | Story US-42 duplicada | USER-STORIES / story dedicada | [#150](https://github.com/JonathanBenicio/Agent-System/issues/150) | multi-key BACK-KEYS-020;US-42 FinOps preservada; caminho histórico mantido |
| R30 | GEMINI orienta Closes indevido | GEMINI / commit-rules | [#150](https://github.com/JonathanBenicio/Agent-System/issues/150) | documentação corrigida em 169c3f8; revisão final pendente |
| R31 | Cypress não acessa servidor frontend | Cypress config / package scripts | [#149](https://github.com/JonathanBenicio/Agent-System/issues/149) | pendente |
| R32 | dt.complete_tasks usa SQL inválido | migration DurableTask opcional | [#151](https://github.com/JonathanBenicio/Agent-System/issues/151) | RepairDurableTaskCompletion; lotes0/1/2/4/rollback/race PostgreSQL16 passaram |

## Critérios de aceite
- [ ] R01–R32 corrigidos ou refutados por evidência concreta, sem substituição por solução que preserve o defeito.
- [ ] Autorização/ACL/FIDES/tenant protegem caminhos reais; testes negativos observam ausência de execução/dados.
- [ ] Limites são uniformes e persistência/estimativas de uso e cancelamento estão documentados.
- [ ] UI/API concordam em tipos de workflow, YAML, tools, sala e sucesso/erro.
- [ ] CI testa contratos atuais e inicia servidores; XSS test não passa por ausência de conteúdo.
- [ ] Banco vazio aplica cadeia, modelo/schema comparados e `dt.complete_tasks` funciona em lotes; nenhum outro banco alterado.
- [ ] Todos os commits em PT-BR têm corpo e issue; PR publicado contra branch do #132.
- [ ] Docs/ADRs/stories/README/INDEX/CONSOLIDATED/tracks e descrições das issues têm estado/evidência coerentes.

## Validação
[Relatório canônico desta correção](../backend/validation/pr132-review-remediation-2026-10-02.md).
Gate: .NET10 Release + regressões direcionadas e suíte completa; frontend lint/build/Playwright/Cypress; PostgreSQL Compose em porta isolada, schema/modelo e probes SQL; checker local de docs e `git diff --check`. Distinguir real, fake, ignorado e não executado; não afirmar integração de provider produção com fake.

## Riscos e decisões
Preservar cookies HttpOnly e raw-key compatível autenticada; Markdown sem HTML ativo; limites ONNX validados também no worker; nunca alterar arbitrariamente tenant/user. Workflow enum mantém valores históricos e contrato string estável; aprovações identificam step e preservam compatibilidade de uma única pendente. Defaults de orçamento/custo ficam explícitos nos contratos e testes.
Mudanças pequenas de correção usam ADR041 para consolidar invariantes existentes, sem nova arquitetura concorrente. Migrations não devem depender de histórico antigo; validação fresca é requisito. Não fechar #97/#111/#113/#115 nem outras issues antigas por subconjunto de critérios.

## Entrega
Commits por contexto, usando caminhos explícitos e preservando alterações alheias. Um PR de correção inicialmente draft aponta à branch do #132; o #132 continua apontando a develop. Sem merge/deploy autorizado. Atualizar esta matriz e relatório a cada contexto concluído.
