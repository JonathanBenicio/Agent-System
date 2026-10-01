# Compatibilidade — Microsoft Agent Framework 1.9.0 → 1.22.0

Data: 2026-09-29 · Issue: [#120](https://github.com/JonathanBenicio/Agent-System/issues/120) · [Plano de execução](maf-122-protocols-gateway.md) · Baseline local: `f941198`.

## Resultado

**Atualização viável, com adaptação obrigatória do armazenamento de sessões.** O build Release da baseline passou com zero avisos/erros. A atualização não pode ser tratada como bump mecânico: as alterações de sessão são breaking e afetam o adaptador próprio do projeto.

## Aderência à plataforma de agentes dinâmica

- **Manter a camada de domínio dinâmica.** O MAF oferece `ChatClientAgent`, `AIFunction`, `WorkflowBuilder` e hosted agents. O projeto armazena definição/configuração de agentes, prompt, tools, habilidades, salas e grafos no catálogo/banco por tenant e materializa esses objetos na execução. O framework executa os agentes/grafos; o catálogo, autorização e editor/runtime de configuração pertencem ao produto.
- **Não duplicar engine de sessão do framework.** O adaptador próprio continua necessário para persistir sessões MAF no `SessionData` PostgreSQL existente e obedecer owner/tenant. A nova chave particionada é mais expressiva, não substitui o backend Postgres do produto.
- **Manter `ContextAwareChatClient` no upgrade.** `RoutePersistingRoutingChatClient` introduzido em MAF 1.19 persiste rota ativa na `AgentSession` e alterna chat clients mantendo histórico client-side. Não substitui seleção por credencial tenant/BYOK, default de tenant, fallback e enforcement/auditoria de quota. Uma evolução pode usá-lo para uma escolha explícita e persistente de rota por sessão, depois de preservar essas regras.
- **Não copiar padrões de hosting de protocolos para o runtime principal.** A `ScopedAgentProxy` cria escopos de execução porque os adapters de protocolo mantêm um agent hospedado; o caminho principal usa factories/services scoped e o catálogo dinâmico. O proxy não deve tornar-se outra camada de orquestração.
- **Risco do DurableTask externo permanece.** `Microsoft.Agents.AI.DurableTask` não tem publicação 1.22; o mais recente é 1.16.0-preview.260922.1. A customização anterior criava `DurableWorkflowClient` via reflection e não passava `DurableOptions`; foi substituída pelo registro público `ConfigureDurableWorkflows(... clientBuilder: UseOrchestrationService)` enquanto grafos JSON por tenant continuam compilados no runtime e passados a `RunAsync(Workflow, ...)`. O novo registro compila, mas precisa resolver/testar `IWorkflowClient` e executar um workflow em PostgreSQL antes de se declarar funcional. O pacote público também mantém risco conhecido de limite de 100 supersteps que pode truncar execução como sucesso; `WorkflowGraphValidator` bloqueia ciclos, mas não profundidade. Não ampliar disponibilidade de grafos duráveis arbitrários sem teste/cap explícito.

## Matriz do código do projeto

| Mudança oficial | Uso encontrado no repositório | Ação necessária |
|---|---|---|
| `AgentSessionStore` movido de Hosting para `Microsoft.Agents.AI`; API usa `AgentSessionStoreKey` e lookup nullable/get-or-create explícito | `SimpleSessionStoreAdapter` deriva da classe anterior; vários serviços/DI declaram o tipo no namespace Hosting | Atualizar namespace/overrides/DI; usar get-or-create nos call sites de execução; testar persistência e retomada |
| `AgentSessionStoreKey` contém ID e partições nomeadas; todas as partições fazem parte da identidade | Adaptador atual recebe apenas string e remove o prefixo legado `tenant:principal::`, descartando dimensões ao normalizar | Persistir estado MAF por ID e todas as partições; testar isolamento; só importar estado antigo após confirmar owner/tenant |
| `SessionIsolationKeyProvider` substituído por `AgentIsolationKeyProvider`/`GetIsolationKeyAsync` | `TenantSessionIsolationKeyProvider` estende a API antiga e produz tenant + principal | Migrar provider; preservar falha fechada sem identidade/tenant; validar execução sem request scope |
| Hosting resolve agente e session store keyed durante `MapAGUIServer` no provider raiz | O store MAF é customizado em `SimpleSessionStoreAdapter`; validação de DI ativa `ValidateScopes=true` | Registrar agent proxy/provider/store em lifetimes compatíveis com route mapping; proxy mantém scopes por execução |
| Replay/aprovação passa a vincular requests estáveis | O sistema usa aprovações próprias e não referencia diretamente tipos MAF de approval/replay | Preservar fluxo do produto; executar testes de aprovação/workflow e corrigir regressões comprovadas |
| Sessões MCP passam a ter escopo por invocação | Tools MCP vêm de `ModelContextProtocol` e adaptador próprio; não foi encontrado uso da hospedagem MCP do MAF | Sem reimplementação MCP neste escopo; build e regressões do adaptador devem passar |
| Extension `AsIChatClient` adicionada | Nenhum uso encontrado; runtime já expõe `ContextAwareChatClient : IChatClient` | Não adotar; validar o caminho atual após M.E.AI atualizado |
| `RoutePersistingRoutingChatClient` permite persistir rota do IChatClient em AgentSession | `LLMManager` + `ContextAwareChatClient` resolvem provider/model por request/session/tenant, chaves BYOK, fallback, quotas e auditoria | Não substituir ainda: recurso do MAF não demonstrou equivalência para políticas tenant/BYOK nem limite/quota. Pode servir para escolha persistente explícita de modelo dentro de um único escopo de credenciais; avaliar como evolução separada |
| MAF dispõe `ChatClientAgent`, `WorkflowBuilder`, `AIFunction` e hosted agents | A fábrica e compiladores do app constroem agents/tools/grafos a partir de catálogo tenant e JSON versionado no banco | Camada custom é a capacidade de produto; o framework já é runtime/executor. Manter definição, autorização e catálogo no produto e evitar duplicar loop/serialização manual de agentes se equivalente API surgir |
| Hosting A2A/AG-UI continua preview | Rotas sob feature flags; o host de validation desliga ambas | Atualizar dependências para compatibilidade/build; E2E está separado em #121 |

## Grafo de pacotes pretendido

- Pacotes core estáveis (`Microsoft.Agents.AI`, `Abstractions`, `Workflows`, `OpenAI`): 1.22.0.
- Hosting (`Hosting`, `Hosting.A2A.AspNetCore`, `Hosting.AGUI.AspNetCore`, `DevUI`): 1.22.0-preview.260918.1. `Microsoft.Agents.AI.DurableTask` não tem versão 1.22 publicada; a mais recente é 1.16.0-preview.260922.1 e declara mínimos `Microsoft.Agents.AI`/`Workflows` 1.16.0, M.E.AI 10.7.0 e DurableTask.Client/Worker 1.18.0. O grafo restaura e compila com MAF 1.22, mas o runtime de workflows duráveis ainda precisa validação PostgreSQL integrada.
- Requisitos publicados: `Microsoft.Extensions.AI`/Evaluation 10.10.0, `Microsoft.Extensions.VectorData.Abstractions` 10.10.0, família Microsoft.Extensions 10.0.12 e OpenTelemetry.Api 1.18.0. Alinhar PackageReferences diretos para evitar downgrade.
- `Microsoft.Extensions.AI.Ollama` não pertence ao MAF; manter sua versão nesta etapa e validar por restore/build e testes do provider. Atualização exige evidência própria.

## Fontes primárias

- [MAF .NET 1.22.0](https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.22.0), [PR #7991: session store compartilhado](https://github.com/microsoft/agent-framework/pull/7991), [PR #8375: approval binding/replay](https://github.com/microsoft/agent-framework/pull/8375) e [PR #8263: hosted session boundaries](https://github.com/microsoft/agent-framework/pull/8263).
- [A2A.AspNetCore 1.22 preview](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting.A2A.AspNetCore/1.22.0-preview.260918.1), [AGUI.AspNetCore 1.22 preview](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/1.22.0-preview.260918.1), [M.E.AI 10.10.0](https://www.nuget.org/packages/Microsoft.Extensions.AI/10.10.0) e [OpenTelemetry 1.18.0](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/1.18.0).

## Validação da análise

- `dotnet build AgenticSystem.sln --configuration Release --no-restore`: passou, zero avisos/erros, baseline 1.9.0.
- Restore com o grafo destino: passou após alinhar Microsoft.Extensions.DependencyInjection de testes para 10.0.12.
- Build Release atualizado: passou com zero avisos/erros depois de migrar o contrato de sessão e os nomes de endpoints AG-UI.
- Teste de mapeamento A2A/AG-UI com `ValidateScopes=true`: 2 passaram.
- Testes focados de sessão, execução direta e colaboração/workflow e Gateway/registry: resultados atualizados estão no plano principal; suíte completa deve ser repetida depois das mudanças de orquestrador.
- DI do `IWorkflowClient` pela extensão MAF e workflow durable real em PostgreSQL: pendentes.
- A2A/AG-UI E2E: fora do caminho crítico e separada em #121; esta análise não certifica protocolo.

## Release .NET 1.23.0 publicada em 2026-10-01 — avaliação para depois de #132

A release contém mudanças relevantes para o catálogo/orquestrador dinâmico e aprovações: suporte a mudanças de tools entre runs, binding de respostas de approval e allow-list de chaves de configuração, todas com mudanças de compatibilidade; ver [release oficial dotnet-1.23.0](https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.23.0). O núcleo `Microsoft.Agents.AI` 1.23.0 já foi publicado como estável, mas `Microsoft.Agents.AI.Hosting` e os hosts A2A/AG-UI continuam em versões `1.23.0-preview`; ver [NuGet do core 1.23.0](https://www.nuget.org/packages/Microsoft.Agents.AI/1.23.0), [Hosting 1.23 preview](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting/1.23.0-preview.260928.1) e [AG-UI 1.23 preview](https://www.nuget.org/packages/Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/1.23.0-preview.260928.1).

**Recomendação:** manter o baseline 1.22 nesta consolidação de tenancy para preservar a validação desta árvore. Planejar o upgrade 1.23 como alteração separada após #132, validando configuração allow-listed, tools do supervisor que mudam entre sessões e fluxos de approval/workflow. A release 1.23 não remove o estado preview de Hosting A2A/AG-UI; o aceite E2E continua em #121.
