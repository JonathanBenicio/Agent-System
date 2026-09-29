# Validação — isolamento e funcionalidades centrais do backend

Data: 2026-09-29 · Commit validado: `151e6d4` · Branch: `fix/backend-core-tenancy` · [Plano](../../plan/backend-core-remediation.md) · [ADR-035](../../architecture/adr/035-backend-core-isolation-and-reliability.md). O relatório histórico de documentação está em [2026-09-28](2026-09-28.md) e não foi sobrescrito.

## Resultado desta rodada

Build Release da solução e harness: zero avisos e zero erros. Validação executada contra PostgreSQL 16.15/pgvector 0.8.6, API Release isolada em loopback e Ollama local; todas as identidades/chaves e os documentos da execução são sintéticos.

| Verificação | Resultado | O que foi exercitado |
|---|---:|---|
| `AgenticSystem.Tests` | 701 aprovados, 1 ignorado, 0 falhas (702 total) | TRX em `tests/TestResults/backend-core-gap-closure/followup/backend-gap-followup.trx`. O único ignorado é a integração PostgreSQL do store no contexto de teste convencional. |
| Integração core HTTP/SignalR | 40/40 aprovados | JWT/API key, memberships, ACL e grants, limites de agentes, upload, RAG real, REST/SSE e os cinco hubs; Gateway viewer/Platform Admin e chamadas cross-tenant. |
| Gateway broadcaster | aprovado | Serviço de fixture no ambiente `Validation`: Platform Admin desabilitou/habilitou e `ServiceStatusChanged` chegou apenas ao grupo do tenant A; tenant B não recebeu os eventos. Viewer recebeu 403 e serviço inexistente retornou 404 sem emitir evento. |
| Store/skills/quotas e backfill | 10/10; backfill aprovado | Busca/room em PostgreSQL, seeding isolado, 32 incrementos em duas factories de contexto/repositórios, bloqueio no teto, reset UTC, ceiling Free e migration partindo do schema legado. |
| Após restart da API | 6/6 aprovados | Mensagens/ownership e estado MAF, quatro skills distintas por tenant, consumo diário persistido, bloqueio real de tokens/custo em REST e OpenAI-compatível (429) e erro terminal em SSE. Rejeições não aumentaram os contadores nem chamaram o LLM. |
| OpenAPI/inventário/links | 204/204 actions visíveis correspondem; 209 rotas fonte sem duplicatas; 665 links sem falhas | OpenAPI inclui respostas 429 para chat REST/OpenAI-compatível e 404 para Gateway; inventário foi gerado novamente do código. |
| Auditoria NuGet | sem vulnerabilidades conhecidas na última verificação | As dependências não mudaram nesta rodada final. |

Os artefatos brutos ficam sob `tests/TestResults/backend-core-gap-closure/run-2026-09-28/` e são ignorados pelo Git. Incluem `core-results.json`, `gateway-results.json`, `session-after-restart.json`, `skills-before-restart.json`, `skills-after-restart.json` e fixtures temporárias. A key usada pelo OpenAI-compatível é aleatória, sintética e fica somente nesse diretório isolado. O banco temporário do backfill foi descartado; volumes locais de validação foram preservados.

## Resultado por gap

| Gap | Estado | Evidência e fronteira |
|---|---|---|
| #111 — role/API key e seleção de tenant | Resolvido nos cenários exercitados | API key usa papel da membership e não recebe Admin implicitamente; identidade/tenant divergente é negada. O auth selector agora envia bearer opaco ao esquema API key, sem selecionar um handler Supabase inexistente. |
| #112 — isolamento HTTP/SignalR e Gateway | Resolvido nos cenários exercitados | Mesmo subject simultâneo em A/B recebeu eventos de Chat, ExternalAgent, Workflow, ONNX e Gateway apenas no escopo esperado. Gateway REST/hub e mutações globais exigem Platform Admin explícito; Viewer é negado. Fixture valida a emissão real do hub/broadcaster, mas o runtime de produção ainda não registra providers em `IServiceGateway`. |
| #113 — RAG com room ACL | Resolvido | Upload em sala autorizada, metadata lógica no PostgreSQL, zero chunks no tenant B e resposta real do chat contendo a frase do chunk autorizado. |
| #114 — isolamento e persistência MAF | Resolvido | REST/SSE/SignalR funcionam; owner/tenant são validados. Após restart, chat retomou o mesmo `sessionId`, preservou mensagens e desserializou o estado MAF persistido. |
| #115 — quota e concorrência | Resolvido para os limites testados | 32 incrementos foram contabilizados exatamente uma vez em duas instâncias de repositório/factory. Chat real persistiu tokens/custo e, após restart, REST e OpenAI-compatível responderam 429 para token/custo excedidos; SSE transmitiu a negação. O RPM do limiter é por processo e não foi testado em dois hosts. |
| #116 — catálogo de skills por tenant | Resolvido | IDs/defaults distintos para tenants A/B, catálogo completo e estável antes/depois do restart, customização preservada. |
| #117 — membership/plano/suporte/migration | Resolvido nos cenários da fixture | Backfill sintético preservou tenant/papel sem criar Platform Admin; GET/PUT/DELETE auditados, roles independentes, support grant expirável/revogável com ACL obrigatória e teto de plano/configuração aplicados. Não é cópia representativa da base de produção. |
| Limites físicos e custo mensal | Limite de medição documentado | Armazenamento mede bytes de origem associados a documento lógico, não o espaço físico total (índices, overhead ou cópias). `MaxMonthlyBudgetUsd` legado continua sendo `MaxDailyCostUsd × 30`, não um acumulador mensal independente. |

O cliente quota-metered verifica um orçamento estimado antes de cada despacho via `IChatClient` e grava uso efetivo do provider após sucesso; se o provider não retornar tokens, usa estimativa conservadora. Abrange chat agentic REST/SSE/SignalR e OpenAI-compatível que usam o orquestrador. `TokenAuditService` é best-effort; `tenant_quotas` permanece a fonte de enforcement. Outros caminhos de chamada direta de provider, fora desse runtime de chat, não foram certificados por esta rodada.

## Cobertura

A última coleta Cobertura conhecida foi 21,08% (12.729/60.359 linhas; 25,71% branches), abaixo do gate de 80% do CI. Essa medição antecede este follow-up e não foi repetida nesta rodada: seguindo a prioridade definida pelo usuário, o trabalho focou em fechar os gaps funcionais e validar seus fluxos reais. O gate de cobertura permanece pendente e o PR continua draft; nenhum limite do CI foi reduzido.

## Reprodução

Com o PostgreSQL/Ollama locais ativos e `BACKEND_VALIDATION_OUTPUT_DIR` apontando para uma pasta isolada:

```powershell
node tests/backend-validation/core-diagnostics.mjs
node tests/backend-validation/gateway-diagnostics.mjs
dotnet tests/backend-validation/bin/Release/net10.0/BackendDiagnostics.dll
dotnet tests/backend-validation/bin/Release/net10.0/BackendDiagnostics.dll --legacy-backfill
dotnet tests/backend-validation/bin/Release/net10.0/BackendDiagnostics.dll --maf-session-fixture
dotnet tests/backend-validation/bin/Release/net10.0/BackendDiagnostics.dll --session-fixture
node tests/backend-validation/session-diagnostics.mjs
# parar e iniciar somente a API isolada
node tests/backend-validation/session-diagnostics.mjs --after-restart
```

Os scripts recusam o diretório histórico `tests/TestResults/backend-documentation/current`, conectam apenas ao compose local em `127.0.0.1:55432`/`127.0.0.1:11435` e usam a API em `127.0.0.1:5188`.
