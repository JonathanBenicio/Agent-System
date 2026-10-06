# Integração com Microsoft Agent Framework

Este guia acompanha o código das branches #132/#152. A fonte arquitetural normativa é [backend-architecture-explained](../docs/architecture/backend-architecture-explained.md); [ADR-036](../docs/architecture/adr/036-maf-122-protocols-and-gateway.md) e [ADR-038](../docs/architecture/adr/038-dynamic-supervisor-orchestrator.md) delimitam as versões e o supervisor.

## Fluxo
`MetaAgentOrchestrator` inicia ou retoma a sessão e encaminha para execução direta ou para o supervisor. `FrameworkOrchestratorService` obtém o catálogo autorizado e `OrchestratorHostBuilder.BuildAsync` constrói um `ChatClientAgent` com especialistas expostos como `AIFunctions`. O LLM decide quais tools invocar; o runtime persiste a sessão MAF separada por tenant e usuário.

A implementação atual usa o supervisor com tools. `BuildHandoffWorkflowAsync`, topologia mesh e streaming por `InProcessExecution`, mencionados nos textos antigos, não descrevem este caminho. O streaming usa `RunStreamingAsync` e é transportado por SSE/SignalR conforme [transportes](../docs/backend/transports.md).

## Pontos de proteção
- `AgentFrameworkFactory` constrói agentes do catálogo e envolve o `IChatClient` com `FidesProtectedChatClient`. O supervisor aplica a mesma proteção.
- `FidesMessageProtection` inspeciona mensagens, instruções e conteúdos de tools antes do provider. Falha de inspeção ou payload incerto bloqueia o despacho.
- `ContextAwareChatClient` seleciona clientes de fallback e envolve chamadas de tenant com `TenantQuotaChatClient`.
- A rota global usa Gateway quando aplicável; BYOK conserva seu próprio cliente/contexto. `CostTracker` registra custo; o teto vem da quota e do plano. `CostTracker` não representa bloqueio global.
- Sessão do supervisor persistida não prova retomada de todas as sessões de especialistas entre hosts.

## Workflows e protocolos
O engine dinâmico recebe definições tenant-owned e executa por worker PostgreSQL com leases e snapshot imutável. Isso é separado da configuração opcional DurableTask; corrigir sua função SQL não registra workflows de produto.

A2A/AG-UI hosting permanece em preview e exige flags e validação específica (#121). Cliente/plugin MCP não implica que exista um servidor `/mcp`. O MAF 1.23 foi analisado, mas os pacotes desta entrega permanecem na versão 1.22.

## Evidência
[Plano de correções](../docs/plan/pr132-review-remediation.md), [ADR-041](../docs/architecture/adr/041-pr132-review-remediation.md) e [relatório](../docs/backend/validation/pr132-review-remediation-2026-10-02.md) distinguem provider fake, PostgreSQL real, UI com API mockada e integrações não executadas. Provider/Gateway de produção (#133) e recuperação do supervisor (#134) permanecem como trabalho futuro.
