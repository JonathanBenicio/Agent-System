# Project Tracks

This file tracks all major tracks for the project. Planos canônicos ficam em docs/plan; cada track referencia seu plano.

---

## Execução atual

| Track | Status | Rastreabilidade |
|---|---|---|
| BACK-REVIEW-139 | Em execução; 32 achados,12 contextos, gate de correção ainda pendente | [#139](https://github.com/JonathanBenicio/Agent-System/issues/139), [plano/matriz](../docs/plan/pr132-review-remediation.md), [ADR-041](../docs/architecture/adr/041-pr132-review-remediation.md), [evidência](../docs/backend/validation/pr132-review-remediation-2026-10-02.md) |
| BACK-DOC-001 | Documentação/diagnóstico consolidados no PR #132; finalizar o plano após merge e revisão | [#110](https://github.com/JonathanBenicio/Agent-System/issues/110), [plano](../docs/plan/backend-documentation-validation.md) |
| BACK-MAF-120 | Implementação parcial no PR #132; validação de provider/Gateway de produção vai para #133 após o merge | [#120](https://github.com/JonathanBenicio/Agent-System/issues/120), [#133](https://github.com/JonathanBenicio/Agent-System/issues/133), [ADR-036](../docs/architecture/adr/036-maf-122-protocols-and-gateway.md), [plano base](../docs/plan/maf-122-protocols-gateway.md), [follow-up](../docs/plan/maf-gateway-production-validation.md) |
| BACK-PROTO-121 | A2A/AG-UI hosting preview; follow-up secundário planejado | [#121](https://github.com/JonathanBenicio/Agent-System/issues/121), [ADR-037](../docs/architecture/adr/037-a2a-agui-preview-validation.md), [plano](../docs/plan/a2a-agui-preview-validation.md) |
| BACK-ORCH-122 | Implementação parcial no PR #132; prova de catálogo/fingerprint e retomada entre processos vai para #134 após o merge | [#122](https://github.com/JonathanBenicio/Agent-System/issues/122), [#134](https://github.com/JonathanBenicio/Agent-System/issues/134), [ADR-038](../docs/architecture/adr/038-dynamic-supervisor-orchestrator.md), [plano base](../docs/plan/dynamic-orchestrator-implementation.md), [follow-up](../docs/plan/dynamic-supervisor-session-recovery.md) |
| BACK-MAF-133 | Provider global em Gateway/produção e reload multi-host; planejado para pós-merge | [#133](https://github.com/JonathanBenicio/Agent-System/issues/133), [story](../docs/USER-STORIES.md#back-maf-133--provider-global-na-composicao-gateway-de-producao), [plano](../docs/plan/maf-gateway-production-validation.md) |
| BACK-ORCH-134 | Sessões/fingerprint do supervisor após restart; planejado para pós-merge | [#134](https://github.com/JonathanBenicio/Agent-System/issues/134), [story](../docs/USER-STORIES.md#back-orch-134--retomar-supervisor-e-especialistas-apos-restart), [plano](../docs/plan/dynamic-supervisor-session-recovery.md) |
| FINOPS-135 | Forecast de quota LLM tenant-scoped planejado, sem substituir enforcement rígido | [#135](https://github.com/JonathanBenicio/Agent-System/issues/135), [ML40](../docs/USER-STORIES.md#ml40--forecast-proativo-de-quotas-llm), [plano](../docs/plan/proactive-llm-quota-forecast.md) |
| TENANT-SCOPE-097 | Parcial após revisão de #132; correções de InMemory e capability na epic #139 | [#97](https://github.com/JonathanBenicio/Agent-System/issues/97), [#143](https://github.com/JonathanBenicio/Agent-System/issues/143), [#145](https://github.com/JonathanBenicio/Agent-System/issues/145), [ADR-026](../docs/architecture/adr/026-auto-bootstrap-remove-default-tenant.md), [plano](../docs/plan/tenant-system-scope-remediation.md) |

## Roadmap histórico Q2 2026 (status histórico, sem nova prova integrada)

| Track ID | Title | Objective | Status | Link |
|:---|:---|:---|:---:|:---|
| `TRK-019` | Specialized Context | Restrict agents to specific Knowledge Rooms | ⏳ Planned | Plano histórico não encontrado; [decisão](../docs/architecture/adr/019-agent-room-association.md) |
| `TRK-008` | FinOps Hub | Tenant-based quota and cost monitoring | ⏳ Planned | [Plan](../docs/plan/master-roadmap-2026.md) |
| `TRK-020` | Protocol Hosting | A2A and AgUI standardization | ⏳ Planned | Plano histórico não encontrado; [contratos atuais](../docs/backend/transports.md) |
| `TRK-021` | Evaluation Suite | Golden Sets and quality metrics | ⏳ Planned | Plano histórico não encontrado; [ADR-032](../docs/architecture/adr/032-evaluation-golden-sets-rest-api.md) |

---
