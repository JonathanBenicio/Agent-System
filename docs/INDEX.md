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
| [../plan/adr-020-protocol-hosting-standardization.md](../plan/adr-020-protocol-hosting-standardization.md) | ADR 020: Padronização e Exposição de Protocolos (A2A e AgUI) |
| [../plan/adr-021-evaluation-framework.md](../plan/adr-021-evaluation-framework.md) | ADR 021: Framework de Avaliação Contínua e Golden Sets |

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

---

## Planejamento & Assessments

Documentos úteis para evolução, diagnóstico e simplificação. Não são a fonte de verdade operacional do runtime atual.

Guia da categoria: [planejamento/README.md](planejamento/README.md).

| Documento | Descrição |
|-----------|-----------|
| [planejamento/AI_Capabilities_Gaps.md](planejamento/AI_Capabilities_Gaps.md) | Diagnóstico vivo de gaps e oportunidades arquiteturais |
| [planejamento/AI_Advanced_Capabilities_Roadmap.md](planejamento/AI_Advanced_Capabilities_Roadmap.md) | Roadmap futuro para capacidades avançadas |
| [planejamento/framework-first-migration-plan.md](planejamento/framework-first-migration-plan.md) | Resumo histórico da migração framework-first (concluída) |
| [planejamento/MAF_NATIVE_REFACTORING.md](planejamento/MAF_NATIVE_REFACTORING.md) | Trilha de redução de código MAF nativo (concluída) |
| [planejamento/master-fullstack-roadmap.md](planejamento/master-fullstack-roadmap.md) | Roadmap fullstack consolidado |
| [planejamento/p2-gateway-observability-finops.md](planejamento/p2-gateway-observability-finops.md) | Gateway observability e FinOps |
| [planejamento/p4-selfhost-ollama-stabilization.md](planejamento/p4-selfhost-ollama-stabilization.md) | Stabilização do Ollama self-hosted |
| [planejamento/agent-yaml-orchestration.md](planejamento/agent-yaml-orchestration.md) | Orquestração via agent.yaml |
| [planejamento/overengineering-assessment.md](planejamento/overengineering-assessment.md) | Assessment de simplificação e hotspots de complexidade |

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
