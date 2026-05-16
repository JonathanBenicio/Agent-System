# Plano: Atualização Completa da Documentação

**Data:** 16 Maio 2026
**Origem:** Auditoria conduzida durante criação/revisão de `AGENTS.md`
**Escopo:** Fases 1+2+3 (correções críticas, remoção de ruído, ajustes menores)

---

## Fase 1 — Correções Críticas (enganam agentes)

### 1.1 README.md — Corrigir exemplo de configuração JSON
**Problema:** Linhas 188-332 mostram estrutura `LLMProviders` achatada, mas o app real usa `AgenticSystem.{Provider}`.
**Ação:** Substituir o exemplo JSON incorreto pela estrutura real extraída de `src/AgenticSystem.Api/appsettings.json`.

### 1.2 README.md — Corrigir link quebrado (linha 421)
**Problema:** Referencia `docs/architecture/design-philosophy.md` que não existe.
**Ação:** Atualizar para o caminho correto ou remover a referência.

### 1.3 .github/copilot-instructions.md — Corrigir link quebrado
**Problema:** Linha 21 referencia `docs/TECHNICAL_ARCHITECTURE_GUIDE.md` (renomeado).
**Ação:** Atualizar para `docs/architecture/backend-architecture-explained.md`.

### 1.4 CONSOLIDATED_DOCS.md — Substituir por índice TOC
**Problema:** 52K+ linhas, concatenação monolítica, links quebrados, cópias obsoletas.
**Ação:** Remover conteúdo massivo e substituir por índice TOC de ~15 linhas com links para os documentos reais.

### 1.5 backend-architecture-explained.md — Ajustar deriva de código
**Problema:** Seção 5.1 mostra registros unconditionais (A2A, AGUI) mas código tem feature flags; menciona `AgentRuntimeCoordinator` que não existe em Program.cs.
**Ação:** Ajustar seção 5.1 para refletir feature flags e nomenclatura real.

---

## Fase 2 — Remoção de Ruído

### 2.1 Remover auditorias redundantes (3 arquivos)
- `docs/docs_review_report.md`
- `docs/relatorio_tecnico_backend_2026.md`
- `docs/relatorio_auditoria_arquitetural_completa.md`

### 2.2 Remover planos transitórios (5 arquivos)
- `implementation_plan.md` (raiz)
- `1.md` (raiz)
- `docs/plan/load_test_results.md`
- `docs/plan/hot-swapping-roadmap.md`
- `docs/plan/refining-rag-foundation.md`
- `docs/plan/load-testing-vector-stores.md`

### 2.3 Arquivar planos concluídos
Mover `docs/plan/completed/*` (7 arquivos) para `docs/old/plans/completed/`

### 2.4 Revisar planos pendentes (16 arquivos)

#### Remover (1)
- `2025-05-22-claude-provider-test-fix.md` (duplicata)

#### Manter em `docs/plan/pending/` (6)
- `claude-provider-test-fix.md`
- `api-key-models-sync.md`
- `auth-httponly-cookie.md`
- `frontend-auth.md`
- `llm-retry-policy.md`
- `self-improvement-async-job.md`

#### Mover para `docs/planejamento/` (4)
- `master-fullstack-roadmap.md`
- `p2-gateway-observability-finops.md`
- `p4-selfhost-ollama-stabilization.md`
- `agent-yaml-orchestration.md`

#### Arquivar em `docs/old/` (5)
- `documentation-cleanup.md` (já concluído — é este plano)
- `llm-circuit-breaker-fix.md` (já implementado por errata)
- `adjust-models-and-catalog.md` (provavelmente resolvido)
- `backend-bug-fixes.md` (bugs corrigidos)
- `configure-cli-extensions.md` (específico Gemini CLI)

### 2.5 Remover duplicata
- `plan/conductor/index.md` (duplica `conductor/index.md`)

### 2.6 Remover audit archive
- `conductor/archive/audit_alignment_20260513/` (3 arquivos)

### 2.7 Mover superpowers plan
- `docs/superpowers/plans/2026-05-14-gemini-429-retry.md` → `docs/plan/pending/`

---

## Fase 3 — Ajustes Menores

### 3.1 REFACTORING_PROGRESS.md
- Mesclar em `MAF_NATIVE_REFACTORING.md` ou remover (já supersedido)

### 3.2 framework-first-migration-plan.md (1204 linhas)
- Podar para apenas resumo histórico (plano concluído)

### 3.3 docs/INDEX.md
- Atualizar índice se arquivos forem removidos/realocados

---

## Arquivos NÃO modificados (manter como estão)
- Todos os 18 ADRs em `docs/architecture/adr/`
- Todos os 19 arquivos `.feature` (BDD)
- Todos os arquivos em `.agents/` (agentes, skills, workflows)
- Glossários (`docs/glossary/`)
- `docs/old/*` (já adequadamente arquivados)
- `docs/referencia-externa/*`
- `conductor/` (exceto audit archive)
- `GEMINI.md`, `src/GEMINI.md`, `AGENTS.md`

---

## Total de arquivos afetados
| Operação | Quantidade |
|----------|-----------|
| Editar (correções) | 5 |
| Remover | ~11 |
| Arquivar (mover para docs/old/) | ~12 |
| Mover entre diretórios | ~5 |
| **Total** | **~33** |
