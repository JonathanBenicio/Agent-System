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
| [architecture/diagrams.md](architecture/diagrams.md) | Diagramas Mermaid alinhados ao runtime atual |
| [TECHNICAL_ARCHITECTURE_GUIDE.md](TECHNICAL_ARCHITECTURE_GUIDE.md) | Guia técnico complementar com detalhamento por subsistema |
| [DSA-AgenticSystem.md](DSA-AgenticSystem.md) | Visão macro de solução, segurança, observabilidade e deploy |
| [architecture/design-philosophy.md](architecture/design-philosophy.md) | Filosofia de design e decisões arquiteturais |
| [architecture/agent-registry.md](architecture/agent-registry.md) | Referência funcional do catálogo de agents |
| [architecture/document-pipeline.md](architecture/document-pipeline.md) | Pipeline de processamento e ingestão de documentos |
| [architecture/rag-flow.md](architecture/rag-flow.md) | Fluxo RAG: retrieval, rerank, budget e contexto |
| [architecture/skills-vs-tools.md](architecture/skills-vs-tools.md) | Contrato de separação entre skills e tools |
| [obsidian-vault.md](obsidian-vault.md) | Subsistema de memória com Obsidian Vault |
| [extension-examples.md](extension-examples.md) | Exemplos de extensão do sistema com agents, tools e skills |

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
| [planejamento/framework-first-migration-plan.md](planejamento/framework-first-migration-plan.md) | Documento transitório: backlog residual da migração framework-first + histórico do cutover |
| [planejamento/MAF_NATIVE_REFACTORING.md](planejamento/MAF_NATIVE_REFACTORING.md) | Documento transitório: trilha de redução de código MAF nativo e rollout residual |
| [planejamento/REFACTORING_PROGRESS.md](planejamento/REFACTORING_PROGRESS.md) | Painel transitório de progresso; fases concluídas funcionam como trilha histórica |
| [planejamento/overengineering-assessment.md](planejamento/overengineering-assessment.md) | Assessment de simplificação e hotspots de complexidade |

---

## Histórico

Artefatos mantidos para rastreabilidade. Não devem ser usados como referência principal do estado atual do sistema.

Guia da categoria: [historico/README.md](historico/README.md).

| Documento | Descrição |
|-----------|-----------|
| [historico/README-old.md](historico/README-old.md) | README anterior do projeto |
| [historico/PIPELINE_REPORT.md](historico/PIPELINE_REPORT.md) | Relatório histórico spec→code; próximos passos internos não são backlog vigente |
| [historico/GAP_ANALYSIS_REPORT.md](historico/GAP_ANALYSIS_REPORT.md) | Análise histórica de gaps do fluxo principal anterior; supersedida por AI_Capabilities_Gaps |

---

## Referência Externa

Material de apoio do fornecedor/framework. Útil para consulta, mas não representa a arquitetura específica do projeto.

Guia da categoria: [referencia-externa/README.md](referencia-externa/README.md).

| Documento | Descrição |
|-----------|-----------|
| [referencia-externa/agent-framework.md](referencia-externa/agent-framework.md) | Referência do Microsoft Agent Framework incorporada ao repositório |
| [referencia-externa/agent-framework.pdf](referencia-externa/agent-framework.pdf) | Versão PDF da referência do Microsoft Agent Framework |
