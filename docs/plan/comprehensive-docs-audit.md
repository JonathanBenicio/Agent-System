status: COMPLETED
tier: TIER 0 / TIER 1
agent: project-planner
updated: 2026-05-14
---

# 📑 Plano de Auditoria Granular e Abrangente de Documentação (Comprehensive Docs Audit Plan)

## 🎯 Overview & Context
Com a expansão expressiva da documentação do monorepo, é indispensável executar uma auditoria analítica e profunda que contemple isoladamente **cada pasta e arquivo** da árvore de `docs/`. O usuário solicitou explicitamente a criação de quantas fases forem necessárias para cobrir todo o escopo de forma metódica e livre de generalizações.

Este plano organiza a varredura em **8 fases estruturais**, separando os artefatos por domínio de responsabilidade (Arquitetura Core, Especificações Executáveis BDD, Planejamento & Roadmaps, Planos de Funcionalidades Ativas, Histórico, Referências Externas e Manifestos Raiz). O objetivo final é desambiguar conceitos, atualizar diagramas, padronizar metadados (frontmatters) e garantir rastreabilidade bidirecional com o código em C# .NET 10 DDD e React 19.

---

## 🏗️ Project Type
**FULLSTACK / ARCHITECTURE** (Governança, Padronização e Blindagem Documental do Sistema).

---

## 🏁 Measurable Success Criteria (AAA Pattern)
1. **Arrange**: Segmentação dos mais de 65 arquivos e diretórios em 8 fases modulares de auditoria, preparando parsers de markdown e esquemas de validação JSON/YAML.
2. **Act**: Verificação sintática e semântica de cada arquivo, atestando a exatidão das descrições, eliminação de menções legadas (ex: Quartz, bibliotecas descontinuadas) e paridade de endpoints/serviços.
3. **Assert**: Sucesso total na validação estrutural cruzada e aprovação sem ressalvas no gate automatizado (`checklist.py`).

---

## ⚖️ Tech Stack & Trade-off Analysis (Socratic Gate)

| Abordagem | Descrição | Prós | Contras | Decisão |
| :--- | :--- | :--- | :--- | :--- |
| **Opção A: Multi-Phase Granular Audit (8 Fases)** | Separação estrita da auditoria por domínio arquitetural, inspecionando cada subdiretório em sua respectiva fase. | Máximo isolamento, permitindo rastrear anomalias específicas de BDD, arquitetura ou planejamento sem poluir o escopo. | Maior extensão documental do plano de tarefas. | **SELECIONADA**. Atende ao rigor de excelência do TIER 0 e ao pedido do usuário. |
| **Opção B: Monolithic Bulk Review (Fase Única)** | Leitura sequencial de todos os arquivos em um único lote de processamento. | Menos verboso no planejamento. | Alto risco de estouro de janela de contexto e perda de atenção a detalhes críticos em arquivos Gherkin/JSON Schema. | Rejeitada. Inadequada para sistemas complexos. |

---

## ⚠️ Risk Assessment & Mitigation

| Risco | Probabilidade | Impacto | Estratégia de Mitigação |
| :--- | :--- | :--- | :--- |
| **Divergência de Padrões entre BDD e PRD** | Alta | Médio | Estabelecer os Manifestos Raiz (`PRD-Sistema-Agentic.md` e `USER-STORIES.md`) como fonte de verdade canônica. |
| **Quebra de Links no Obsidian/Índices** | Média | Alto | Validar todos os links relativos de `INDEX.md` e `obsidian-vault.md` com scripts de varredura pré e pós-auditoria. |

---

## 📁 File Structure & Mapping (Escopo Integral)

```markdown
docs/
├── architecture/                  # [Fase 1: Contratos, Esquemas e Diagramas]
├── bdd/                           # [Fase 2 & 3: Especificações Gherkin e Cenários]
├── planejamento/                  # [Fase 4: Roadmaps, Gaps e Maturidade]
├── plan/                          # [Fase 5: Funcionalidades Ativas e Superpowers]
├── historico/                     # [Fase 6: Relatórios de Auditoria e Legado]
├── referencia-externa/            # [Fase 6: Fornecedor MAF e Frameworks]
└── (Arquivos Raiz)                # [Fase 7: Manifestos, PRD e Visão Global]
```

---

## 📋 Detalhamento das Tarefas de Auditoria (Task Breakdown)

### Phase 1: ARCHITECTURE CORE (Contratos, Esquemas e Diagramas)

#### Task 1.1: Auditoria do Registry e Explicação Arquitetural
- [x] **Agent**: `backend-specialist` | **Skill**: `api-patterns`
- **Priority**: P0 | **Dependencies**: Nenhuma
- **Input**: `docs/architecture/agent-registry.json`, `agent-registry.md`, `agent-registry.schema.json`, `backend-architecture-explained.md`.
- **Output**: Esquema JSON estritamente validado e documentação de backend alinhada com as entidades reais em C#.
- **Verify**: Validação de schema JSON via linter estático.

#### Task 1.2: Auditoria dos Fluxos e Pipelines
- [x] **Agent**: `project-planner` | **Skill**: `architecture`
- **Priority**: P0 | **Dependencies**: Task 1.1
- **Input**: `docs/architecture/diagrams.md`, `document-pipeline.md`, `rag-flow.md`, `skills-vs-tools.md`.
- **Output**: Diagramas Mermaid revisados, fluxos de RAG/Pipeline livres de ambiguidades e distinção clara entre Skills e Tools.
- **Verify**: Renderização bem-sucedida de todos os blocos Mermaid.

---

### Phase 2: BDD CORE (Cenários de Chat, Autenticação e Gestão)

#### Task 2.1: Inspeção de Cenários de Gestão de Agentes e Autenticação
- [x] **Agent**: `test-engineer` | **Skill**: `testing-patterns`
- **Priority**: P1 | **Dependencies**: Task 1.2
- **Input**: `docs/bdd/agent-management.feature`, `agent-nao-encontrado.feature`, `api-key-masking-embedding.feature`, `autenticacao-multi-scheme.feature`, `backend-apis.feature`, `chat-dedicado-via-lista.feature`, `chat-interface.feature`, `contrato-api-chat-dedicado.feature`.
- **Output**: Cenários Gherkin padronizados, semântica `Given/When/Then` verificada e cobertura de autenticação multi-scheme garantida.
- **Verify**: Execução de parser de Gherkin confirmando sintaxe sem erros.

---

### Phase 3: BDD ADVANCED (Realtime, Plugins, Routing e Configurações)

#### Task 3.1: Inspeção de Cenários de Integração, MCP e Roteamento
- [x] **Agent**: `test-engineer` | **Skill**: `testing-patterns`
- **Priority**: P1 | **Dependencies**: Task 2.1
- **Input**: `docs/bdd/embedding-migration.feature`, `gateway-dashboard.feature`, `historico-separado-por-agent.feature`, `llm-providers.feature`, `mcp-plugins.feature`, `mensagem-direto-ao-agent.feature`, `rate-limiting-chat.feature`, `README.md`, `scheduled-tasks.feature`, `settings-config.feature`, `signalr-realtime.feature`, `transversal-ux.feature`, `voltar-roteamento-automatico.feature`.
- **Output**: Integrações MCP, fluxos de SignalR Realtime e rate limiting completamente descritos em conformidade com o backend.
- **Verify**: Inspeção cruzada de paridade com `ISmartRouter` e serviços de infraestrutura.

---

### Phase 4: ROADMAPS & CAPABILITIES (Planejamento de Maturidade e Refatoração)

#### Task 4.1: Auditoria de Capabilities e Análise de Gaps
- [x] **Agent**: `project-planner` | **Skill**: `plan-writing`
- **Priority**: P1 | **Dependencies**: Task 3.1
- **Input**: `docs/planejamento/Agent_Runtime_State_Machine.md`, `AI_Advanced_Capabilities_Roadmap.md`, `AI_Capabilities_Gaps.md`, `enterprise_gap_analysis.md`, `framework-first-migration-plan.md`, `overengineering-assessment.md`, `README.md`.
- **Output**: Avaliação de overengineering calibrada, matriz de gaps atualizada e máquina de estados de runtime validada.
- **Verify**: Consistência de marcos evolutivos com o roadmap global.

#### Task 4.2: Auditoria dos Checkpoints de Refatoração MAF
- [x] **Agent**: `project-planner` | **Skill**: `architecture`
- **Priority**: P1 | **Dependencies**: Task 4.1
- **Input**: `docs/planejamento/MAF_NATIVE_REFACTORING.md`, `REFACTORING_CHECKPOINT_PHASE1.md` até `PHASE4.md`, `REFACTORING_PROGRESS.md`.
- **Output**: Rastreamento impecável das fases de refatoração nativa do Microsoft Agent Framework.
- **Verify**: Confirmação de status de progresso documentado contra as branches do git.

---

### Phase 5: ACTIVE FEATURE PLANS (Planos de Funcionalidades e Superpowers)

#### Task 5.1: Auditoria dos Planos de Features Ativas
- [x] **Agent**: `orchestrator` | **Skill**: `plan-writing`
- **Priority**: P2 | **Dependencies**: Task 4.2
- **Input**: `docs/plan/2025-05-22-claude-provider-test-fix.md`, `adjust-models-and-catalog.md`, `agent-yaml-orchestration.md`, `api-key-models-sync.md`, `auth-httponly-cookie.md`, `backend-bug-fixes.md`, `claude-provider-test-fix.md`, `claude.md`, `configure-cli-extensions.md`, `docs-audit-sync.md`, `frontend-auth.md`, `llm-circuit-breaker-fix.md`, `llm-retry-policy.md`, `master-fullstack-roadmap.md`, `p1-rag-maf-ecosystem.md`, `p2-gateway-observability-finops.md`, `p3-extensibility-automation.md`, `p4-selfhost-ollama-stabilization.md`.
- **Output**: Padronização estrita de cabeçalhos (frontmatters com `status:`, `tier:` e `agent:`), arquivamento de tarefas já entregues e desambiguação de escopo.
- **Verify**: Inspecionar que nenhum plano contenha status indefinido ou conflitante.

#### Task 5.2: Auditoria dos Planos de Superpowers Extensions
- [x] **Agent**: `orchestrator` | **Skill**: `documentation-templates`
- **Priority**: P2 | **Dependencies**: Task 5.1
- **Input**: `docs/superpowers/plans/2026-05-14-gemini-429-retry.md`.
- **Output**: Validação de diretrizes de resiliência (HTTP 429) e extensão de linter.
- **Verify**: Checagem de paridade com o motor de superpowers.

---

### Phase 6: HISTORICAL & EXTERNAL REFERENCE (Rastreabilidade e Fornecedores)

#### Task 6.1: Auditoria do Histórico de Auditorias e Relatórios
- [x] **Agent**: `security-auditor` | **Skill**: `documentation-templates`
- **Priority**: P2 | **Dependencies**: Task 5.2
- **Input**: `docs/historico/DI_RUNTIME_AUDIT.md`, `GAP_ANALYSIS_REPORT.md`, `PIPELINE_REPORT.md`, `README-old.md`, `README.md`.
- **Output**: Preservação intacta do histórico de auditoria de injeção de dependência e relatórios de pipeline, com marcação de obsolescência explícita onde aplicável.
- **Verify**: Atestar integridade de leitura dos relatórios.

#### Task 6.2: Auditoria de Referências Externas do MAF
- [x] **Agent**: `backend-specialist` | **Skill**: `documentation-templates`
- **Priority**: P2 | **Dependencies**: Task 6.1
- **Input**: `docs/referencia-externa/agent-framework.md`, `agent-framework.pdf`, `README.md`.
- **Output**: Contratos de referência externa validados contra a versão ativa do Microsoft Agent Framework utilizada no backend.
- **Verify**: Checagem de integridade dos links de documentação externa.

---

### Phase 7: ROOT MANIFESTS & GLOBAL INDEX (Visão Canônica Global)

#### Task 7.1: Auditoria dos Manifestos de Design e Histórias de Usuário
- [x] **Agent**: `project-planner` | **Skill**: `documentation-templates`
- **Priority**: P0 | **Dependencies**: Task 6.2
- **Input**: `docs/agentic-design-manifesto.md`, `docs_review_report.md`, `DSA-AgenticSystem.md`, `extension-examples.md`, `obsidian-vault.md`, `PRD-Sistema-Agentic.md`, `USER-STORIES.md`.
- **Output**: Manifestos, PRD e histórias de usuário consolidados como a fonte absoluta de verdade funcional do produto.
- **Verify**: Garantir que todas as histórias de usuário tenham identificadores rastreáveis únicos.

#### Task 7.2: Atualização e Sincronização Canônica do `INDEX.md`
- [x] **Agent**: `orchestrator` | **Skill**: `clean-code`
- **Priority**: P0 | **Dependencies**: Task 7.1
- **Input**: `docs/INDEX.md` e todas as subpastas.
- **Output**: Índice mestre completamente atualizado, contendo apontamentos exatos e funcionais para cada um dos 65+ arquivos auditados.
- **Verify**: Teste automatizado de resolução de links relativos.

---

### Phase 8: ROLLOUT & VALIDATION GATE (Execução de Checklist Mestre)

#### Task 8.1: Execução da Automação de Cobertura Documental
- [x] **Agent**: `orchestrator` | **Skill**: `deployment-procedures`
- **Priority**: P0 | **Dependencies**: Task 7.2
- **Input**: Árvore documental completa e perfeitamente inspecionada.
- **Output**: Passagem 100% limpa no gate de automação de CI/CD.
- **Verify**: Execução final de `python .agents/scripts/checklist.py .`.

---

## 🔄 Rollback Strategy
* **Mecanismo**: Criação de ponto de restauração via git tag (`pre-docs-audit-v2`) antes do início das alterações. Em caso de anomalia, reverte-se para a tag.
* **Critério de Restauração**: Falha em qualquer etapa de validação sintática ou quebra de links no `INDEX.md`.

---

## 🏁 Phase X: Final Verification (Checklist & Automation Gate)

```bash
python .agents/scripts/checklist.py .
```

## ✅ PHASE X COMPLETE
- Lint: ✅ Pass
- Security: ✅ No critical issues
- Build & Coverage: ✅ Success
- Date: 2026-05-14
