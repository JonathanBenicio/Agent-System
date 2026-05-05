# Documentação — AgenticSystem

Índice de navegação para toda a documentação do projeto.

---

## Produto & Requisitos

| Documento | Descrição |
|-----------|-----------|
| [PRD-Sistema-Agentic.md](PRD-Sistema-Agentic.md) | Product Requirements Document — visão de produto para stakeholders |
| [USER-STORIES.md](USER-STORIES.md) | Catálogo completo de User Stories (ML1–ML33 backend + US-01–US-33 frontend) |
| [agentic-design-manifesto.md](agentic-design-manifesto.md) | 10 princípios de design do sistema |

---

## Arquitetura

| Documento | Descrição |
|-----------|-----------|
| [architecture/design-philosophy.md](architecture/design-philosophy.md) | Filosofia de design e decisões arquiteturais |
| [architecture/agent-registry.md](architecture/agent-registry.md) | Registry de agents — formato, validação e lifecycle |
| [architecture/diagrams.md](architecture/diagrams.md) | Diagramas Mermaid (C4, sequência, componentes) |
| [architecture/document-pipeline.md](architecture/document-pipeline.md) | Pipeline de processamento de documentos |
| [architecture/rag-flow.md](architecture/rag-flow.md) | Fluxo RAG — ingestão, chunking, retrieval |
| [architecture/skills-vs-tools.md](architecture/skills-vs-tools.md) | Diferença entre Skills e Tools no sistema |

### Schemas

| Arquivo | Descrição |
|---------|-----------|
| [architecture/agent-registry.json](architecture/agent-registry.json) | JSON Schema do agent registry |
| [architecture/agent-registry.schema.json](architecture/agent-registry.schema.json) | JSON Schema (formato alternativo) |

---

## BDD — Cenários de Teste

Cenários Gherkin para validação funcional. Ver [bdd/README.md](bdd/README.md) para instruções de execução.

| Feature | US Relacionada |
|---------|---------------|
| [chat-dedicado-via-lista.feature](bdd/chat-dedicado-via-lista.feature) | US-31 |
| [mensagem-direto-ao-agent.feature](bdd/mensagem-direto-ao-agent.feature) | US-32 |
| [contrato-api-chat-dedicado.feature](bdd/contrato-api-chat-dedicado.feature) | US-32 |
| [historico-separado-por-agent.feature](bdd/historico-separado-por-agent.feature) | US-33 |
| [voltar-roteamento-automatico.feature](bdd/voltar-roteamento-automatico.feature) | US-33 |
| [agent-nao-encontrado.feature](bdd/agent-nao-encontrado.feature) | US-32 (edge case) |

---

## Extensão & Integração

| Documento | Descrição |
|-----------|-----------|
| [extension-examples.md](extension-examples.md) | Exemplos de código para estender o sistema (agents, skills, tools) |
| [obsidian-vault.md](obsidian-vault.md) | Subsistema de memória com Obsidian Vault |

---

## Arquitetura & Governança

| Documento | Descrição |
|-----------|-----------|
| [TECHNICAL_ARCHITECTURE_GUIDE.md](TECHNICAL_ARCHITECTURE_GUIDE.md) | Guia técnico detalhado — Orchestrator, Agent Framework, LLM, RAG, MCP, Multi-Tenant, Auth, SignalR, Gateway, Frontend React |
| [AI_Capabilities_Gaps.md](AI_Capabilities_Gaps.md) | Diagnóstico vivo de gaps, itens parciais, backlog e capacidades fechadas recentemente |
| [architecture/overengineering-assessment.md](architecture/overengineering-assessment.md) | Avaliação de over-engineering com hotspots, impacto e plano de simplificação |
| [DSA-AgenticSystem.md](DSA-AgenticSystem.md) | Documento de Solução de Arquitetura (segurança, observabilidade, deploy) |
| [../PIPELINE_REPORT.md](../PIPELINE_REPORT.md) | Pipeline Reports — In-Memory Persistence + Architectural Review & Security Fixes |

---

## Histórico

| Documento | Descrição |
|-----------|-----------|
| [historico/README-old.md](historico/README-old.md) | README anterior (deprecated) |
