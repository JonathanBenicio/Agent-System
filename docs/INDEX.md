# Documentação — AgenticSystem

Índice de navegação da documentação do projeto, organizado por papel documental.

Documento canônico de arquitetura atual:
- [architecture/backend-architecture-explained.md](architecture/backend-architecture-explained.md) — fonte de verdade do runtime backend revalidada contra o código.

---

## Documentação Viva

### Produto & Requisitos

| Documento | Descrição |
|-----------|-----------|
| [PRD-Sistema-Agentic.md](PRD-Sistema-Agentic.md) | Visão de produto para stakeholders e áreas de negócio |
| [USER-STORIES.md](USER-STORIES.md) | Catálogo funcional consolidado de MLs, épicos e user stories |
| [user-stories/us-skills-crud-and-brainstorm.md](user-stories/us-skills-crud-and-brainstorm.md) | US-012: Gestão Dinâmica de Skills via Tela e Brainstorming assistido por IA |
| [user-stories/us-multi-provider-api-keys.md](user-stories/us-multi-provider-api-keys.md) | US-42: Gerenciamento e Roteamento de Múltiplas API Keys |
| [user-stories/unified-chat-integration.md](user-stories/unified-chat-integration.md) | US-021: Integração Unificada de Pilares Tecnológicos no Chat Principal |
| [user-stories/us-async-onnx-processing.md](user-stories/us-async-onnx-processing.md) | US-43: Processamento Assíncrono de Inferência ONNX |
| [user-stories/us-advanced-chat-session-management.md](user-stories/us-advanced-chat-session-management.md) | US-44: Chat Avançado e Gerenciamento Unificado de Sessões |
| [user-stories/us-032-dynamic-maf-workflows-engine.md](user-stories/us-032-dynamic-maf-workflows-engine.md) | US-032: Orquestração Dinâmica de Grafos de Agentes e Compilador MAF Declarativo |
| [issue-61-multi-provider-api-keys.md](issue-61-multi-provider-api-keys.md) | GitHub Issue #61: Multi-Provider API Keys Epic |
| [issue-chat-enhancements.md](issue-chat-enhancements.md) | GitHub Issue #93: Chat Avançado Epic |
| [agentic-design-manifesto.md](agentic-design-manifesto.md) | Princípios de design e filosofia do sistema |

### Arquitetura Corrente

| Documento | Descrição |
|-----------|-----------|
| [architecture/backend-architecture-explained.md](architecture/backend-architecture-explained.md) | Arquitetura canônica do backend atual, framework-first/hosted |
| [architecture/concepts.md](architecture/concepts.md) | Conceitos aplicados e justificativas (.NET/IA/MAF) |
| [architecture/diagrams.md](architecture/diagrams.md) | Diagramas Mermaid alinhados ao runtime atual |
| [DSA-AgenticSystem.md](DSA-AgenticSystem.md) | Visão macro de solução, segurança, observabilidade e deploy |
| [architecture/agent-registry.md](architecture/agent-registry.md) | Referência funcional do catálogo de agents |
| [architecture/document-pipeline.md](architecture/document-pipeline.md) | Pipeline de processamento e ingestão de documentos |
| [architecture/rag-flow.md](architecture/rag-flow.md) | Fluxo RAG: retrieval, rerank, budget e contexto |
| [architecture/skills-vs-tools.md](architecture/skills-vs-tools.md) | Contrato de separação entre skills e tools |
| [architecture/smart-routing-triage.md](architecture/smart-routing-triage.md) | Arquitetura de Smart Triage (3 camadas) e Fast Path |
| [obsidian-vault.md](obsidian-vault.md) | Subsistema de memória com Obsidian Vault |
| [extension-examples.md](extension-examples.md) | Exemplos de extensão do sistema com agents, tools e skills |

### Decisões Arquiteturais (ADRs)

| Documento | Descrição |
|-----------|-----------|
| [architecture/adr/001-retrospective-dotnet10-ddd.md](architecture/adr/001-retrospective-dotnet10-ddd.md) | ADR 001: Retrospectiva sobre .NET 10 e DDD |
| [architecture/adr/002-smart-triage-fast-path.md](architecture/adr/002-smart-triage-fast-path.md) | ADR 002: Implementação de Smart Triage e Fast Path |
| [architecture/adr/003-hierarchical-agent-framework.md](architecture/adr/003-hierarchical-agent-framework.md) | ADR 003: Framework de Agentes Hierárquicos |
| [architecture/adr/004-transactional-outbox.md](architecture/adr/004-transactional-outbox.md) | ADR 004: Padrão Transactional Outbox |
| [architecture/adr/005-pgvector-graph-rag.md](architecture/adr/005-pgvector-graph-rag.md) | ADR 005: Graph RAG com pgvector |
| [architecture/adr/006-ollama-self-hosting.md](architecture/adr/006-ollama-self-hosting.md) | ADR 006: Self-Hosting com Ollama |
| [architecture/adr/007-microsoft-agent-framework.md](architecture/adr/007-microsoft-agent-framework.md) | ADR 007: Microsoft Agent Framework |
| [architecture/adr/008-quota-monitoring-finops.md](architecture/adr/008-quota-monitoring-finops.md) | ADR 008: Monitoramento de Quotas e FinOps |
| [architecture/adr/009-dual-auth-security.md](architecture/adr/009-dual-auth-security.md) | ADR 009: Segurança com Autenticação Dupla |
| [architecture/adr/010-onnx-runtime-in-process.md](architecture/adr/010-onnx-runtime-in-process.md) | ADR 010: ONNX Runtime In-Process |
| [architecture/adr/011-exponential-backoff-resilience.md](architecture/adr/011-exponential-backoff-resilience.md) | ADR 011: Resiliência com Exponential Backoff |
| [architecture/adr/012-multi-tenant-agent-memory-schema.md](architecture/adr/012-multi-tenant-agent-memory-schema.md) | ADR 012: Schema de Memória Multi-Tenant |
| [architecture/adr/013-nginx-websocket-proxy.md](architecture/adr/013-nginx-websocket-proxy.md) | ADR 013: Proxy WebSocket com Nginx |
| [architecture/adr/014-multi-llm-provider-architecture.md](architecture/adr/014-multi-llm-provider-architecture.md) | ADR 014: Arquitetura Multi-LLM |
| [architecture/adr/015-model-context-protocol-mcp.md](architecture/adr/015-model-context-protocol-mcp.md) | ADR 015: Model Context Protocol (MCP) |
| [architecture/adr/016-unified-agent-structure.md](architecture/adr/016-unified-agent-structure.md) | ADR 016: Estrutura Unificada de Agentes |
| [architecture/adr/017-design-system-teal-palette.md](architecture/adr/017-design-system-teal-palette.md) | ADR 017: Design System com Paleta Teal |
| [architecture/adr/018-hot-swapping-architecture.md](architecture/adr/018-hot-swapping-architecture.md) | ADR 018: Arquitetura de Hot-Swapping para Provedores de IA e Vector Stores |
| [architecture/adr/019-agent-room-association.md](architecture/adr/019-agent-room-association.md) | ADR 019: Associação Granular Agente-Sala (Contexto Restrito) |
| [architecture/adr/020-multi-provider-api-keys.md](architecture/adr/020-multi-provider-api-keys.md) | ADR 020: Arquitetura de Múltiplas API Keys por Provedor LLM |
| [architecture/adr/021-automatic-llm-discovery.md](architecture/adr/021-automatic-llm-discovery.md) | ADR 021: Inspeção Automática de Modelos LLM no Login (Issue #94) |
| [architecture/adr/022-unified-chat-integration-architecture.md](architecture/adr/022-unified-chat-integration-architecture.md) | ADR 022: Arquitetura de Integração Unificada do Chat (12 Pilares) |
| [architecture/adr/023-async-onnx-processing.md](architecture/adr/023-async-onnx-processing.md) | ADR 023: Processamento Assíncrono de Inferência ONNX e Galeria |
| [architecture/adr/024-backend-architectural-refactoring.md](architecture/adr/024-backend-architectural-refactoring.md) | ADR 024: Refatoração Arquitetural e Limpeza Técnica do Backend (.NET 10) |
| [architecture/adr/025-advanced-chat-session-management.md](architecture/adr/025-advanced-chat-session-management.md) | ADR 025: Chat Avançado e Gerenciamento Unificado de Sessões (NotebookLM Style) |
| [architecture/adr/027-maf-1-6-1-migration-architecture.md](architecture/adr/027-maf-1-6-1-migration-architecture.md) | ADR 027: Migração Completa para o Microsoft Agent Framework (MAF) 1.6.1 |
| [architecture/adr/028-db-skills-dynamic-system.md](architecture/adr/028-db-skills-dynamic-system.md) | ADR 028: Implementação do DbAgentSkillsSource e CRUD de Skills via Tela |
| [architecture/adr/029-dynamic-maf-workflows-engine.md](architecture/adr/029-dynamic-maf-workflows-engine.md) | ADR 029: Orquestração Dinâmica de Grafos de Agentes baseada no MAF |
| [architecture/adr/030-maf-durable-task-migration.md](architecture/adr/030-maf-durable-task-migration.md) | ADR 030: Workflows, sessões PostgreSQL e resposta HTTP assíncrona |
| [architecture/adr/031-pgvector-sql-filter-tenant-quota-persistence.md](architecture/adr/031-pgvector-sql-filter-tenant-quota-persistence.md) | ADR 031: Filtro SQL-Nativo no PGVector e Persistência de Quotas de Tenant |
| [architecture/adr/032-evaluation-golden-sets-rest-api.md](architecture/adr/032-evaluation-golden-sets-rest-api.md) | ADR 032: Contratos REST para CRUD de Golden Sets da Evaluation Suite |
| [architecture/adr/033-maf-190-upgrade-di-cleanup.md](architecture/adr/033-maf-190-upgrade-di-cleanup.md) | ADR 033: Migração do Microsoft Agent Framework para 1.9.0 e Saneamento de DI |
| [architecture/adr/ADR-004-MAF-Native-LLM-Clients.md](architecture/adr/ADR-004-MAF-Native-LLM-Clients.md) | ADR 004: Migração para Clientes Nativos do Microsoft Agent Framework (MAF 1.6+) |
| [architecture/adr/ADR-005-MAF-Native-Workflows.md](architecture/adr/ADR-005-MAF-Native-Workflows.md) | ADR 005: Migração para Workflows Nativos do Microsoft Agent Framework (MAF 1.6+) |
| [architecture/adr/ADR-006-MAF-Native-Skills.md](architecture/adr/ADR-006-MAF-Native-Skills.md) | ADR 006: Padronização do Skills Framework com MAF Nativo |

### Glossários

| Documento | Descrição |
|-----------|-----------|
| [glossary/technical.md](glossary/technical.md) | Glossário Técnico: Termos e acrônimos de engenharia |
| [glossary/business.md](glossary/business.md) | Glossário de Domínio: Termos de negócio e agentes |

### Artefatos Estruturados

| Arquivo | Descrição |
|---------|-----------|
| [architecture/agent-registry.json](architecture/agent-registry.json) | Dados estruturados do catálogo de agents |
| [architecture/agent-registry.schema.json](architecture/agent-registry.schema.json) | Schema formal de validação do catálogo |

### BDD — Cenários Executáveis

Ver [bdd/README.md](bdd/README.md) para o inventário completo e instruções de execução.

| Feature | Escopo |
|---------|--------|
| [bdd/chat-interface.feature](bdd/chat-interface.feature) | Chat principal e seleção de IA |
| [bdd/chat-dedicado-via-lista.feature](bdd/chat-dedicado-via-lista.feature) | Entrada em chat dedicado |
| [bdd/mensagem-direto-ao-agent.feature](bdd/mensagem-direto-ao-agent.feature) | Bypass direto para agent alvo |
| [bdd/contrato-api-chat-dedicado.feature](bdd/contrato-api-chat-dedicado.feature) | Contrato REST/SignalR do chat dedicado |
| [bdd/signalr-realtime.feature](bdd/signalr-realtime.feature) | Streaming e reconexão em tempo real |
| [bdd/backend-apis.feature](bdd/backend-apis.feature) | APIs de documentos, planner, voz, setup e memória |
| [bdd/embedding-migration.feature](bdd/embedding-migration.feature) | Módulo administrativo ativo de migração de embeddings |
| [bdd/api-key-masking-embedding.feature](bdd/api-key-masking-embedding.feature) | Mascaramento de segredos no módulo de embeddings |
| [bdd/multi-provider-api-keys.feature](bdd/multi-provider-api-keys.feature) | BDD: Cenários executáveis para múltiplas API Keys |
| [bdd/async-onnx-processing.feature](bdd/async-onnx-processing.feature) | Processamento Assíncrono de Inferência ONNX |
| [bdd/advanced-chat-session-management.feature](bdd/advanced-chat-session-management.feature) | Chat Avançado e Gerenciamento Unificado de Sessões (NotebookLM Style) |

---

## Planejamento & Assessments

Documentos úteis para evolução, diagnóstico e simplificação. Não são a fonte de verdade operacional do runtime atual.

Guia da categoria: [planejamento/README.md](planejamento/README.md).

| Documento | Descrição |
|-----------|-----------|
| [plan/pending-changes-review-2026-09-28.md](plan/pending-changes-review-2026-09-28.md) | Revisão de alterações pendentes e validações de 28/09/2026 |
| [plan/di-upgrade-1-9-0.md](plan/di-upgrade-1-9-0.md) | Implementação e verificação da atualização MAF 1.9.0 e DI |
| [plan/golden-set-api.md](plan/golden-set-api.md) | Plano de CRUD e avaliação de Golden Sets |
| [plan/phase-3-backend-gaps.md](plan/phase-3-backend-gaps.md) | Complementos de Golden Sets e execução assíncrona |
| [plan/backend-architecture-audit.md](plan/backend-architecture-audit.md) | Roadmap/Plan: Backend Architectural Audit & Technical Diagnosis Report |
| [plan/phase-1-critical-fixes.md](plan/phase-1-critical-fixes.md) | Roadmap/Plan: Fase 1 — Correções Críticas de Segurança e Corretude |
| [plan/phase-2-maf-migration.md](plan/phase-2-maf-migration.md) | Roadmap/Plan: Fase 2 — Saneamento de DI e Migração MAF 1.9.0 |
| [plan/phase-3-roadmap-completion.md](plan/phase-3-roadmap-completion.md) | Roadmap/Plan: Fase 3 — Completar Tracks do Roadmap Q2 2026 |
| [plan/completed/onnx-in-process.md](plan/completed/onnx-in-process.md) | Roadmap/Plan: Dynamic ONNX In-Process Inference Engine (Issue #74) |
| [plan/completed/automatic-llm-inspection-plan.md](plan/completed/automatic-llm-inspection-plan.md) | Roadmap/Plan: Inspeção Automática de Modelos LLM no Login (Issue #94) |
| [plan/gap-mitigation-plan.md](plan/gap-mitigation-plan.md) | Roadmap/Plan: Mitigação de Gaps Técnicos de Segurança e Performance (Issues #76, #77, #78, #79) |
| [plan/bug-chat-workflow.md](plan/bug-chat-workflow.md) | Bug Fix Plan: Resposta do chat não aparece no frontend |
| [plan/multi-provider-api-keys.md](plan/multi-provider-api-keys.md) | Roadmap/Plan: Multi-Provider API Keys |
| [plan/completed/unified-chat-integration.md](plan/completed/unified-chat-integration.md) | Roadmap/Plan: Integração Unificada do Chat (12 Pilares) |
| [plan/completed/async-onnx-processing-plan.md](plan/completed/async-onnx-processing-plan.md) | Roadmap/Plan: Processamento Assíncrono de Inferência ONNX e Galeria |
| [plan/backend-testing-roadmap.md](plan/backend-testing-roadmap.md) | Roadmap/Plan: Plano e Roteiro de Testes do Backend (.NET 10) |
| [plan/completed/architectural-refactoring-plan.md](plan/completed/architectural-refactoring-plan.md) | Roadmap/Plan: Refatoração Arquitetural e Limpeza Técnica do Backend (.NET 10) |
| [plan/completed/chat-enhancements.md](plan/completed/chat-enhancements.md) | Roadmap/Plan: Chat Avançado e Gerenciamento Unificado de Sessões (NotebookLM Style) (Issue #93) |
| [plan/completed/db-skills-dynamic-system.md](plan/completed/db-skills-dynamic-system.md) | Roadmap/Plan: Implementação do DbAgentSkillsSource e Dynamic Skills System |
| [plan/dynamic-maf-workflows-engine.md](plan/dynamic-maf-workflows-engine.md) | Roadmap/Plan: Orquestração Dinâmica de Grafos de Agentes (Abordagem B) |
| [plan/ci-pipelines.md](plan/ci-pipelines.md) | Roadmap/Plan: Configuração de Pipelines de CI (GitHub Actions) |
| [plan/frontend-refactoring-plan.md](plan/frontend-refactoring-plan.md) | Roadmap/Plan: Refatoração do Frontend (React Query, useChat decomposition) |
| [plan/master-roadmap-2026.md](plan/master-roadmap-2026.md) | Master Roadmap Q2 2026: Entregáveis e prioridades estratégicas |
| [plan/maintenance-frontend.md](plan/maintenance-frontend.md) | Guia/Plan: Diretrizes de Manutenção Visual e UX do Frontend |
| [plan/completed/backend-dependency-injection-refactoring.md](plan/completed/backend-dependency-injection-refactoring.md) | Roadmap/Plan: Refatoração de Injeção de Dependência no Backend (Issue #101) |
| [plan/completed/ARCHITECTURE_REFACTORING_PLAN.md](plan/completed/ARCHITECTURE_REFACTORING_PLAN.md) | Roadmap/Plan: Refatoração Arquitetural do Sistema e Gaps de I/O |
| [plan/completed/maf-migration-phase1.md](plan/completed/maf-migration-phase1.md) | Roadmap/Plan: Transição de Habilidades Estáticas para Microsoft Agent Framework |
| [plan/completed/remediation-backend.md](plan/completed/remediation-backend.md) | Roadmap/Plan: Remediação de DI e Segurança do Backend |
| [plan/completed/strategic-alignment.md](plan/completed/strategic-alignment.md) | Guia/Plan: Alinhamento Estratégico do Core vs Lab |
| [plan/completed/controle-acesso-rooms.md](plan/completed/controle-acesso-rooms.md) | Roadmap/Plan: Controle de Acesso e Permissões em Knowledge Rooms |
| [plan/completed/documentation-update-plan.md](plan/completed/documentation-update-plan.md) | Roadmap/Plan: Atualização Completa e Faxina da Documentação |
| [plan/completed/gaps-telas.md](plan/completed/gaps-telas.md) | Roadmap/Plan: Diagnóstico e Resolução de Gaps nas Telas de UI |
| [plan/completed/claude-provider-test-fix.md](plan/completed/claude-provider-test-fix.md) | Bug Fix Plan: Teste de Conectividade de Provedores LLM (Claude) |
| [plan/completed/cap-suggestions-ptbr.md](plan/completed/cap-suggestions-ptbr.md) | Roadmap/Plan: Sugestões de Capacidades em PT-BR com Dicas Visuais |
| [plan/completed/architectural-refactoring-phase-2.md](plan/completed/architectural-refactoring-phase-2.md) | Roadmap/Plan: Refatoração Arquitetural e Limpeza do Backend - Fase 2 |
| [plan/completed/refactoring-session-list.md](plan/completed/refactoring-session-list.md) | Roadmap/Plan: Refinamento de Listagem de Sessões de Usuário |
| [plan/completed/p2-gateway-observability-finops.md](plan/completed/p2-gateway-observability-finops.md) | Roadmap/Plan: Gateway Observability & FinOps (Fase P2) |
| [plan/completed/p4-selfhost-ollama-stabilization.md](plan/completed/p4-selfhost-ollama-stabilization.md) | Roadmap/Plan: Estabilização e Self-Hosting com Ollama (Fase P4) |
| [plan/completed/agent-yaml-orchestration.md](plan/completed/agent-yaml-orchestration.md) | Roadmap/Plan: Editor de Agentes Declarativo com Suporte a YAML e Validação |
| [plan/completed/MAF_NATIVE_REFACTORING.md](plan/completed/MAF_NATIVE_REFACTORING.md) | Roadmap/Plan: Trilha de Refatoração e Aproximação ao MAF Nativo |
| [plan/completed/framework-first-migration-plan.md](plan/completed/framework-first-migration-plan.md) | Roadmap/Plan: Resumo Histórico da Migração Framework-First |
| [plan/completed/REFACTORING_PROGRESS.md](plan/completed/REFACTORING_PROGRESS.md) | Roadmap/Plan: Consolidação e Progresso de Refatoração do MAF Native Runtime |
| [planejamento/AI_Capabilities_Gaps.md](planejamento/AI_Capabilities_Gaps.md) | Diagnóstico vivo de gaps e oportunidades arquiteturais |
| [planejamento/AI_Advanced_Capabilities_Roadmap.md](planejamento/AI_Advanced_Capabilities_Roadmap.md) | Roadmap futuro para capacidades avançadas |
| [planejamento/master-fullstack-roadmap.md](planejamento/master-fullstack-roadmap.md) | Roadmap fullstack consolidado |
| [planejamento/overengineering-assessment.md](planejamento/overengineering-assessment.md) | Assessment de simplificação e hotspots de complexidade |
| [planejamento/Agent_Runtime_State_Machine.md](planejamento/Agent_Runtime_State_Machine.md) | Diagnóstico: Análise de Estado de Execução e Práticas Enterprise-Grade |

---

## Histórico

Artefatos mantidos para rastreabilidade. Não devem ser usados como referência principal do estado atual do sistema.

| Documento | Descrição |
|-----------|-----------|
| [old/README-old.md](old/README-old.md) | README anterior do projeto |
| [old/PIPELINE_REPORT.md](old/PIPELINE_REPORT.md) | Relatório histórico spec→code |
| [old/GAP_ANALYSIS_REPORT.md](old/GAP_ANALYSIS_REPORT.md) | Análise histórica de gaps do fluxo principal anterior |
| [old/DI_RUNTIME_AUDIT.md](old/DI_RUNTIME_AUDIT.md) | Auditoria de runtime DI (Histórico) |
| [old/REFACTORING_CHECKPOINT_PHASE1.md](old/REFACTORING_CHECKPOINT_PHASE1.md) | Checkpoint de Refatoração - Fase 1 |
| [old/REFACTORING_CHECKPOINT_PHASE2.md](old/REFACTORING_CHECKPOINT_PHASE2.md) | Checkpoint de Refatoração - Fase 2 |

---

## Referência Externa

Material de apoio do fornecedor/framework. Útil para consulta, mas não representa a arquitetura específica do projeto.

Guia da categoria: [referencia-externa/README.md](referencia-externa/README.md).

| Documento | Descrição |
|-----------|-----------|
| [referencia-externa/agent-framework.md](referencia-externa/agent-framework.md) | Referência do Microsoft Agent Framework incorporada ao repositório |
| [referencia-externa/agent-framework.pdf](referencia-externa/agent-framework.pdf) | Versão PDF da referência do Microsoft Agent Framework |
