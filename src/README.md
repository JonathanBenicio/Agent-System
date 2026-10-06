# AgenticSystem — backend

Guia de navegação do código, atualizado no contexto da [epic #139](https://github.com/JonathanBenicio/Agent-System/issues/139). A arquitetura canônica está em [backend-architecture-explained](../docs/architecture/backend-architecture-explained.md); os contratos operacionais e limites de evidência estão no [hub backend](../docs/backend/README.md).

## Projetos e execução
- AgenticSystem.Api: controllers, autenticação, SignalR e mapeamentos de host.
- AgenticSystem.Core: contratos, catálogo de agentes, sessões, quotas e engine dinâmico.
- AgenticSystem.Infrastructure: MAF, providers, stores, PostgreSQL, Gateway, RAG e serviços externos.
- AgenticSystem.Tests: regressões; o resultado depende de SHA, configuração e serviços disponíveis.

Código em .NET 10, com MAF core 1.22 e hosting preview. Providers configuráveis não significam que os modelos estejam homologados para produção. A análise do MAF 1.23 é planejamento separado; esta entrega não atualiza os pacotes.

Configure o host conforme [operações](../docs/backend/operations.md) e o exemplo versionado; não copie credenciais das fixtures. Um banco novo recebe as migrations durante a inicialização. A validação PostgreSQL usa exclusivamente o Compose isolado na porta 55432 e uma base temporária. O requisito de banco vazio não autoriza apagar dados de outros projetos.

## Runtime vigente
[MetaAgentOrchestrator](AgenticSystem.Core/Services/MetaAgentOrchestrator.cs) coordena chat, triagem e sessões. O caminho do supervisor usa [FrameworkOrchestratorService](AgenticSystem.Infrastructure/AgentFramework/FrameworkOrchestratorService.cs) e [OrchestratorHostBuilder.BuildAsync](AgenticSystem.Infrastructure/AgentFramework/OrchestratorHostBuilder.cs): o supervisor MAF invoca especialistas por tools. O método `BuildHandoffWorkflowAsync` e o grafo mesh descritos na versão anterior deste guia não fazem parte do fluxo atual.

Chamadas diretas usam [AgentFrameworkDirectExecutionService](AgenticSystem.Infrastructure/AgentFramework/AgentFrameworkDirectExecutionService.cs) e a mesma factory protegida. FIDES envolve o `IChatClient` antes de cada despacho ao provider. Sessões MAF são particionadas por tenant e usuário. Workflows definidos pelo usuário usam o engine/store dinâmico e leases; DurableTask permanece opcional.

## Acesso, recursos e limites
Tenant real, membership e ACL de sala são invariantes independentes. Operações globais exigem uma capability interna de sistema. Cookie HttpOnly autentica o navegador sem guardar API key em `localStorage`; JWT explícito é outro modo de autenticação. Consulte [acesso](../docs/backend/access-tenants.md).

`ContextAwareChatClient` resolve provider, modelo e quota por tenant. Credenciais BYOK não passam pelo Gateway global. `CostTracker` registra o custo; `QuotaEnforcer` e o cliente de quota aplicam o teto efetivo. Não atribua bloqueio de orçamento ao `CostTracker` nem afirme que todas as chamadas usam Gateway. Consulte [recursos e limites](../docs/backend/resources-rules.md).

## Verificação e pendências
[Plano R01–R32](../docs/plan/pr132-review-remediation.md) e [relatório de validação por contexto](../docs/backend/validation/pr132-review-remediation-2026-10-02.md) registram implementação, doubles, PostgreSQL real, testes ignorados e gates pendentes. Provider/Gateway de produção (#133), retomada multi-host (#134) e protocolos preview (#121) continuam como trabalho futuro. Um teste unitário ou provider fake não demonstra essas integrações.
