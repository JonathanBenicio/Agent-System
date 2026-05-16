# Plano de Migração: Framework-First Orchestration — RESUMO HISTÓRICO

> **[CONCLUÍDO]** Este arquivo contém apenas o resumo histórico da migração framework-first.
> A arquitetura operacional vigente está em [../architecture/backend-architecture-explained.md](../architecture/backend-architecture-explained.md).

**Período:** Maio 2026 | **Status:** 100% Concluído

## Objetivo

Inverter controle de orquestração do `AgentExecutionWorkflow` imperativo para o Microsoft Agent Framework (MAF) nativo como runtime principal.

## Fases Executadas

| Fase | Objetivo | Status |
|------|----------|--------|
| 1 | Centralizar entrada no Framework | ✅ |
| 2 | Mover cross-cutting concerns para o Framework | ✅ |
| 3 | Remover duplicidade arquitetural | ✅ |
| 4 | Protocol Hosting e interoperabilidade (A2A, AG-UI) | ✅ |

## Resultado

- Runtime opera 100% sobre MAF 1.5.0 com `AddAIAgent()` hosting nativo
- `ScopedAgentProxy` resolve conflito Singleton/Scoped para protocolos
- `OrchestratorHostBuilder` substituiu `OrchestratorContextFactory`
- Protocolos A2A e AG-UI sob feature flags (`ProtocolHosting`)
- MCP server temporariamente desabilitado (aguardando compatibilidade)

## Referências

- [MAF Native Refactoring](MAF_NATIVE_REFACTORING.md) — trilha de redução de código transitório
- [Backend Architecture](../architecture/backend-architecture-explained.md) — documento canônico do runtime
