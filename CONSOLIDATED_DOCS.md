# CONSOLIDATED_DOCS.md

> Contratos operacionais atuais: [hub do backend](docs/backend/README.md). Evidências: [validação](docs/backend/validation/2026-09-28.md).
> Correções da revisão: [PR #118](docs/backend/validation/2026-09-28-review-fixes.md).
> Correção de backend #111–#117: [ADR-035](docs/architecture/adr/035-backend-core-isolation-and-reliability.md), [plano/status](docs/plan/backend-core-remediation.md) e [evidências atuais](docs/backend/validation/backend-core-remediation.md).
> Issue #120 — MAF 1.22 e Gateway: [ADR-036](docs/architecture/adr/036-maf-122-protocols-and-gateway.md), [story](docs/USER-STORIES.md#back-maf-120--maf-atualizado-e-providers-integrados-ao-gateway), [análise de compatibilidade](docs/plan/maf-122-compatibility-review.md) e [plano](docs/plan/maf-122-protocols-gateway.md).
> Issue #121 — A2A/AG-UI em hosting preview, follow-up não bloqueante: [ADR-037](docs/architecture/adr/037-a2a-agui-preview-validation.md), [story](docs/USER-STORIES.md#back-proto-121--validar-a2a-e-ag-ui-sob-hosting-preview) e [plano](docs/plan/a2a-agui-preview-validation.md).
> BACK-ORCH-122 — supervisor dinâmico implementado: [issue #122](https://github.com/JonathanBenicio/Agent-System/issues/122), [ADR-038](docs/architecture/adr/038-dynamic-supervisor-orchestrator.md), [story](docs/USER-STORIES.md#back-orch-122--orquestrar-agentes-dinamicos-pelo-supervisor-maf), [plano](docs/plan/dynamic-orchestrator-implementation.md) e [evidência de runtime](docs/backend/validation/maf-122-workflow-runtime-2026-09-29.md). Sessão MAF reaberta após reinício real da API; recuperação de Wait demonstrada. Banner gerou arquivo final com client MAF determinístico e skills reais; inferência vision/editor segue sem teste.
> Issue #16 — auto-melhoria com flag Lab desligada por padrão, aprovação Owner/Admin, versionamento, auditoria e rollback: contrato em [API do backend](docs/backend/api-core.md).

Este arquivo foi substituído por um índice de navegação. Consulte a documentação canônica diretamente:

- **Arquitetura**: [docs/architecture/backend-architecture-explained.md](docs/architecture/backend-architecture-explained.md)
- **Índice geral**: [docs/INDEX.md](docs/INDEX.md)
- **Product Requirements**: [docs/PRD-Sistema-Agentic.md](docs/PRD-Sistema-Agentic.md)
- **User Stories**: [docs/USER-STORIES.md](docs/USER-STORIES.md)
- **Master Roadmap Q2 2026**: [plan/master-roadmap-2026.md](docs/plan/master-roadmap-2026.md)
- **Backend Architecture Audit**: [docs/plan/backend-architecture-audit.md](docs/plan/backend-architecture-audit.md)
- **Fase 1 — Correções Críticas**: [docs/plan/phase-1-critical-fixes.md](docs/plan/phase-1-critical-fixes.md)
- **Fase 2 — Migração MAF 1.9.0**: [docs/plan/phase-2-maf-migration.md](docs/plan/phase-2-maf-migration.md)
- **Fase 3 — Roadmap Q2 2026**: [docs/plan/phase-3-roadmap-completion.md](docs/plan/phase-3-roadmap-completion.md)
- **ADR 031 — PGVector SQL Filter + Quota Persistence**: [docs/architecture/adr/031-pgvector-sql-filter-tenant-quota-persistence.md](docs/architecture/adr/031-pgvector-sql-filter-tenant-quota-persistence.md)
- **ADR 032 — Golden Sets REST API**: [docs/architecture/adr/032-evaluation-golden-sets-rest-api.md](docs/architecture/adr/032-evaluation-golden-sets-rest-api.md)
- **ADR 033 — MAF 1.9.0 Upgrade + DI Cleanup**: [docs/architecture/adr/033-maf-190-upgrade-di-cleanup.md](docs/architecture/adr/033-maf-190-upgrade-di-cleanup.md)
- **Design Manifesto**: [docs/agentic-design-manifesto.md](docs/agentic-design-manifesto.md)
- **BDD Scenarios**: [docs/bdd/README.md](docs/bdd/README.md)
- **Planejamento**: [docs/planejamento/README.md](docs/planejamento/README.md)

- **Revisão das alterações pendentes (28/09/2026)**: [docs/plan/pending-changes-review-2026-09-28.md](docs/plan/pending-changes-review-2026-09-28.md)
