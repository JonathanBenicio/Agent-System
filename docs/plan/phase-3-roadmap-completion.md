# Roadmap: Fase 3 — Completar Tracks do Roadmap Q2 2026

> **Status documental:** Backend implementado; critérios complementares pendentes
> **Escopo:** Implementar funcionalidades faltantes identificadas na auditoria para completar as Tracks 1, 2 e 4 do Roadmap Q2 2026.
> **Fonte de verdade operacional:** [Backend Architecture Audit](backend-architecture-audit.md) · [Master Roadmap 2026](master-roadmap-2026.md)
> **Gerado em:** 04 de Junho de 2026
> **Projeto:** AgenticSystem

---

## Objetivo

Fechar os gaps de implementação identificados na auditoria arquitetural, completando as entregas pendentes do Q2 2026 que foram documentadas mas não implementadas: CRUD de Golden Sets para a Track 4 de Avaliação Contínua, e a atualização da documentação técnica das Tracks 1 e 2 para refletir o estado real após as correções das Fases 1 e 2.

Esta fase deve ser executada **após as Fases 1 e 2**, uma vez que:
- Track 1 (RAG/Rooms) depende da correção do filtro PGVector (Fase 1).
- Track 2 (FinOps) depende da persistência de quotas (Fase 1).
- Track 4 (Evaluation) é independente e pode ser paralela à Fase 2.

## Princípios de Implantação

1. **API-first** — definir contratos OpenAPI/Swagger antes de implementar controllers.
2. **Tenant-isolated** — todos os endpoints devem respeitar o `X-Tenant-Id` e as políticas de acesso do tenant.
3. **Consistência de padrão** — seguir os mesmos padrões REST dos controllers existentes (response envelopes, paginação, error handling com `X-Correlation-Id`).
4. **Testes E2E** — cada endpoint novo deve ter testes Cypress ou Playwright validando o fluxo completo.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Golden Set CRUD API (Track 4) | Independente — pode iniciar imediatamente |
| 2 | Atualização de docs Track 1 (RAG) | Após Fase 1 corrigir o filtro PGVector |
| 3 | Atualização de docs Track 2 (FinOps) | Após Fase 1 implementar persistência de quotas |
| 4 | Sincronização de índices e governança | Sempre por último |

---

## Detalhamento 1: Golden Set CRUD API (Track 4)

### Por que implementar?

A suíte de avaliação contínua (`RuntimeEvaluatorService`) está operacional mas **não possui endpoints REST** para gerenciar os Golden Sets (conjuntos de casos de teste com input/output esperado). Sem CRUD, os datasets só podem ser gerenciados diretamente no banco — inviável em produção.

### Arquitetura-alvo

```
POST   /api/evaluation/golden-sets          → Criar golden set
GET    /api/evaluation/golden-sets          → Listar golden sets do tenant (paginado)
GET    /api/evaluation/golden-sets/{id}     → Detalhar golden set
PUT    /api/evaluation/golden-sets/{id}     → Atualizar golden set
DELETE /api/evaluation/golden-sets/{id}     → Remover golden set
POST   /api/evaluation/golden-sets/{id}/run → Executar avaliação contra um golden set
```

### Componentes propostos

| Componente | Papel |
|---|---|
| `GoldenSetController` | Controller REST com os 6 endpoints acima |
| `GoldenSetEntity` | Entidade EF Core: `Id`, `TenantId`, `Name`, `Description`, `Cases[]`, `CreatedAt` |
| `GoldenSetCase` | Value object: `Input` (string), `ExpectedOutput` (string), `Tags` (string[]) |
| `IGoldenSetRepository` | Abstração de persistência tenant-aware |
| `GoldenSetRepository` | Implementação com EF Core + query filter por `TenantId` |
| `GoldenSetDto` / `CreateGoldenSetDto` | DTOs para request/response (sem expor entidade diretamente) |
| Migration EF Core | Criar tabelas `GoldenSets` e `GoldenSetCases` |

### Plano por etapas

1. **Definir DTOs** — `CreateGoldenSetDto`, `UpdateGoldenSetDto`, `GoldenSetDto`, `GoldenSetCaseDto`.
2. **Criar entidades** `GoldenSetEntity` e `GoldenSetCase` implementando `ITenantEntity`.
3. **Criar migration** EF Core: `dotnet ef migrations add AddGoldenSets ...`.
4. **Implementar** `IGoldenSetRepository` e `GoldenSetRepository` com global query filter por `TenantId`.
5. **Implementar** `GoldenSetController` com os 6 endpoints, usando `[Authorize]` e `X-Tenant-Id`.
6. **Integrar** o endpoint `POST .../run` com `RuntimeEvaluatorService.EvaluateAsync`.
7. **Registrar** serviços no DI container (`ServiceCollectionExtensions`).
8. **Escrever testes** unitários do controller (NSubstitute para repository) e de integração (WebApplicationFactory).
9. **Atualizar Swagger/OpenAPI** com exemplos e descriptions para os novos endpoints.

### Critérios de Aceite e SLOs

* [x] CRUD completo de Golden Sets via REST, com isolamento por tenant.
* [x] Paginação em `GET /api/evaluation/golden-sets` com `page` e `pageSize`.
* [x] `POST .../run` dispara avaliação e retorna score + detalhe dos casos passados/falhados.
* [ ] Testes unitários do controller com ≥ 80% cobertura das branches.
* [ ] Swagger documentado com exemplos de request/response.
* [ ] `docs/planejamento/Agent_Runtime_State_Machine.md` atualizado com especificação dos endpoints.

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| `RuntimeEvaluatorService.EvaluateAsync` pode demorar (LLM call) | Tornar `/run` assíncrono com polling via `GET /api/evaluation/runs/{runId}` |
| Golden Sets podem ser grandes (muitos casos) | Limitar `Cases` a 500 por set via validação no DTO |

---

## Detalhamento 2: Atualização Documental — Track 1 (RAG/Rooms)

### Por que implementar?

Após a correção do filtro PGVector (Fase 1), a documentação arquitetural atual **afirma incorretamente** que a filtragem por `room_ids` ocorre no banco de dados. Isso precisa ser atualizado para refletir o estado real.

### Plano por etapas

1. **Atualizar** `docs/architecture/backend-architecture-explained.md` — seção sobre `KnowledgeSpecialist` e `PostgresVectorStore`, indicando que a filtragem é agora SQL-nativa (pós-fix).
2. **Atualizar** `docs/architecture/adr/019-agent-room-association.md` — adicionar nota de implementação confirmando que o filtro SQL foi aplicado.
3. **Verificar** `docs/USER-STORIES.md` para stories relacionadas à Track 1 e marcar como implementadas.

### Critérios de Aceite

* [x] Documentação técnica não contém mais afirmações de filtragem in-memory.
* [x] ADR-019 marcado como `Aprovado` e com nota de implementação.

---

## Detalhamento 3: Atualização Documental — Track 2 (FinOps)

### Por que implementar?

Após a implementação da persistência de quotas (Fase 1), o `ProactiveQuotaManager` deve ser documentado como ativo, e as referências a `ConcurrentDictionary` em memória devem ser removidas da documentação.

### Plano por etapas

1. **Atualizar** `docs/planejamento/p2-gateway-observability-finops.md` — confirmar que `ProactiveQuotaManager` é implementado via PostgreSQL + IMemoryCache.
2. **Atualizar** `docs/architecture/adr/008-quota-monitoring-finops.md` — adicionar nota de implementação da Fase 1.
3. **Verificar** `docs/USER-STORIES.md` para stories de FinOps e marcar as implementadas.

### Critérios de Aceite

* [x] Documentação de FinOps reflete a implementação real (PostgreSQL + cache).
* [ ] ADR-008 atualizado com referência cruzada para ADR-031.

---

## Detalhamento 4: Sincronização de Índices e Governança

### Plano por etapas

1. **Atualizar** `docs/INDEX.md` com os novos planos de fase e ADRs criados.
2. **Atualizar** `CONSOLIDATED_DOCS.md` com links para os novos arquivos.
3. **Atualizar** `README.md` com status atualizado das 4 tracks do Roadmap Q2 2026.
4. **Verificar** `AGENTS.md` — confirmar que nenhuma instrução precisa ser atualizada após as mudanças de arquitetura.

### Critérios de Aceite

* [x] `docs/INDEX.md` lista todos os planos de fase e ADRs novos (031, 032, 033).
* [x] `CONSOLIDATED_DOCS.md` atualizado com todos os novos documentos.
* [ ] `README.md` reflete o status real de que as Tracks 1, 2, 3 e 4 estão concluídas (✅).

## Verificação posterior

Consulte [a revisão de 28/09/2026](pending-changes-review-2026-09-28.md) para os testes executados, cobertura medida e pendências operacionais. As marcações de implementação não representam validação em produção.
