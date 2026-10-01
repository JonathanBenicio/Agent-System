# Plano — Corrigir isolamento e funcionalidades centrais

Status: gaps funcionais #111–#117 implementados e validados nos cenários locais; gate global de cobertura continua abaixo do requisito de CI · Issues: #111–#117 · [ADR-035](../architecture/adr/035-backend-core-isolation-and-reliability.md) · Stories: BACK-FIX-111–117.

Branch: `fix/backend-core-tenancy` · base do PR: `docs/backend-contracts-review` · baseline da execução final: `008109e`.

## Escopo e decisões

Implementar e documentar isolamento tenant, membership e papéis, grants de suporte, RAG, estado MAF, quotas e limites de recursos; validar em PostgreSQL/pgvector/Ollama isolados. Preservar tenant/papel na migration sem promover ninguém a Platform Admin. Plano é teto; configuração pode restringir. Não fazer merge/deploy nem alterar frontend. `.gitignore` staged e arquivos/pastas pessoais preexistentes permanecem fora dos commits desta correção.

## Implementação e evidência

- [x] #111 — API key usa role da membership; papel desconhecido e override divergente falham fechado. Bearer opaco é roteado para o handler de API key sem exigir esquema Supabase não configurado.
- [x] #112 — validação HTTP/SignalR e grupos por tenant; Chat, ExternalAgent, Workflow, ONNX e Gateway testados com o mesmo subject em A/B. Gateway dashboard e mutations globais exigem Platform Admin, service inexistente retorna 404, status broadcaster foi exercitado por WebSocket real e viewer negado.
- [x] #113 — filtro SQL tenant/room, lista vazia fail-closed, upload com ACL e chat RAG real com artifact referenciando o chunk autorizado.
- [x] #114 — após restart, chat MAF retomou o mesmo sessionId; mensagens, owner, tenant e estado serializado restaurados.
- [x] #115 — incremento PostgreSQL atômico, 32 increments concorrentes por dois repositórios/factories, custo/tokens reais persistidos; após restart, token/custo excedidos retornaram 429 sem aumentar contadores; SSE trouxe erro de quota.
- [x] #116 — defaults de skills com IDs tenant-scoped, customizações preservadas e catálogo de quatro skills de A/B completo e estável após restart.
- [x] #117 — memberships GET/PUT/DELETE admin-only e auditadas; grants Reader auditáveis/expiráveis/revogáveis com ACL; backfill legado preserva tenant/papel sem promoção; limites usam Tenant.Limits.
- [x] Gateway global — operações REST/hub e mutações de estado protegidas por registro Platform Admin. Fixture de serviço existe só no ambiente Validation.
- [x] Contratos — endpoint inventory e OpenAPI regenerados contra o código; links e respostas 429 sincronizados.
- [ ] Cobertura CI — último valor conhecido 21,08%, abaixo do mínimo de 80%; não reduzi o threshold. Por orientação do usuário, esta rodada priorizou fechar e validar gaps funcionais.

## Verificação final

Build Release da solução/harness: zero avisos e erros. Suíte: 701 aprovados, 1 ignorado, 0 falhas (702 total). Integração core: 43/43; Gateway broadcast: aprovado; store/quota/skills: 10/10; backfill legado: aprovado; sessão pós-restart: 6/6. PostgreSQL 16.15/pgvector 0.8.6 e Ollama reais, com tenants/identidades/documentos sintéticos.

Relatório discriminado: [backend-core-remediation](../backend/validation/backend-core-remediation.md). Validação documental histórica: [2026-09-28](../backend/validation/2026-09-28.md). Artefatos da execução: `tests/TestResults/backend-core-gap-closure/run-2026-09-28/`, ignorados pelo Git.

## Limitações e release

Bytes de origem são contabilidade lógica de documentos associados a vetores, não espaço físico total. `MaxMonthlyBudgetUsd` legado é uma projeção de custo diário × 30. RPM permanece local por processo; múltiplos hosts de API não foram testados. A fixture de backfill é sintética. O Gateway fixture valida autorização e emissão do evento no código, mas o runtime de produção ainda não registra integrations de providers no registry Gateway. `TokenAuditService` é best-effort; `tenant_quotas` é a fonte do enforcement. O host de validação não habilitou A2A/AG-UI.

Manter PR #119 em draft enquanto o gate obrigatório de 80% não passar. As issues ficam abertas para revisão humana; não fechar, fazer merge ou deploy sem confirmar os critérios e o gate aplicável.
