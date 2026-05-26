# Master Roadmap Q2 2026: Agentic System Evolution

**Goal:** Transition from a robust agentic core to a specialized, interoperable, and observable platform.

## Track 1: Specialized Context (Agents 🤝 Rooms)
*   **Objective:** Restrict agent knowledge to specific rooms to ensure multi-tenant safety and domain focus.
*   **Key Deliverables:**
    *   ADR: Agent-Knowledge Room Association.
    *   UI: Room selector in `AgentFormModal`.
    *   Backend: Permission-aware document retrieval in `KnowledgeSpecialist`.
*   **Target Files:** `docs/architecture/adr/019-agent-room-association.md`, `docs/USER-STORIES.md`, `plan/agent-room-integration.md`.

## Track 2: FinOps & Observability Hub
*   **Objective:** Comprehensive cost management and real-time quota visibility.
*   **Key Deliverables:**
    *   ADR-008 Expansion: Tenant Quotas & Forecasting.
    *   UI: Dedicated FinOps page with charts (Token usage, Cost projection).
    *   Backend: `ProactiveQuotaManager` enforcement.
*   **Target Files:** `docs/architecture/adr/008-quota-monitoring-finops.md`, `docs/planejamento/p2-gateway-observability-finops.md`, `plan/finops-dashboard.md`.

## Track 3: Protocol Interoperability (A2A & AgUI)
*   **Objective:** Expose agents via standardized protocols for cross-system collaboration.
*   **Key Deliverables:**
    *   ADR: Standardized Protocol Hosting Bridge.
    *   UI: Protocol management dashboard.
    *   Backend: Validation of `ScopedAgentProxy` for protocol-based sessions.
*   **Target Files:** `AGENTS.md`, `docs/architecture/backend-architecture-explained.md`, `plan/protocol-hosting-validation.md`.

## Track 4: Continuous Evaluation Suite
*   **Objective:** Measure and improve agent performance using automated Golden Sets.
*   **Key Deliverables:**
    *   ADR: Evaluation Framework Architecture.
    *   UI: Evaluation dashboard (Score tracking).
    *   Backend: Integration with `Microsoft.Extensions.AI.Evaluation`.
*   **Target Files:** `docs/planejamento/Agent_Runtime_State_Machine.md`, `docs/USER-STORIES.md`, `plan/evaluation-suite.md`.

---

## Global Sync Checklist
Para cada track acima, os seguintes arquivos globais devem ser atualizados:
- [ ] `docs/INDEX.md` (Novo ADR e Plano)
- [ ] `CONSOLIDATED_DOCS.md` (Novos links)
- [ ] `README.md` (Update no status do roadmap)
- [ ] `GEMINI.md` (Novas instruções de contexto se necessário)
- [ ] `conductor/tracks.md` (Registrar novas tracks de trabalho)
