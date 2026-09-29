# Plano — Implementar o orquestrador supervisor dinâmico

Status: plano separado do upgrade MAF/Gateway; issue #122 criada e vinculada a ADR-038, story e este plano. Última suíte Release com Compose isolado: 735 aprovados, 1 teste vetorial explicitamente ignorado, 0 falhas; build Release sem avisos/erros. A regressão funcional do orquestrador passou; teste de snapshot confirmou persistência e reabertura MAF após recriar adapter no PostgreSQL e negou outro tenant. Restart real do processo/host continua pendente. Story: BACK-ORCH-122 · [ADR-038](../architecture/adr/038-dynamic-supervisor-orchestrator.md). Depende da API de sessão MAF 1.22 em [#120](maf-122-protocols-gateway.md).

## Objetivo

Fazer a rota inteligente do chat executar o `ChatClientAgent` supervisor com specialists do catálogo como tools MAF, mantendo seleção dinâmica por tenant, sessões, segurança e contratos atuais do chat.

## Estado encontrado

O backend já tem `OrchestratorHostBuilder`, `OrchestratorToolBindingService`, `AgentFrameworkFactory`, `FrameworkOrchestratorService` e `ChatClientAgent`; não é um orquestrador greenfield. O gap é tornar esse caminho coerente: binding e grafo de handoff competem, o `IAgent` do catálogo não é `AIAgent`, o wrapper de workflow não recebe a sessão MAF criada para o request, e somente a sessão do supervisor é persistida. O cache de instruções usa apenas nomes, portanto edição dinâmica de configuração pode deixar prompt obsoleto.

## Etapas

| Entrega | Verificação | Estado |
|---|---|---|
| Separar os especialistas efetivamente bindados dos agentes ativos e usar apenas bindings no prompt/cache | Provider que falha ao criar tool não aparece como delegável; mudança de descrição/domínio/tier/tools muda fingerprint | Teste funcional confirma especialista indisponível omitido das tools MAF expostas; teste dedicado prova fingerprint muda por descrição/domínio/tools |
| Executar supervisor diretamente com a AgentSession tenant/user-scoped | Fake MAF/IChatClient chama ferramentas e retorna resposta; sessões supervisor/especialistas ficam serializadas | Regressão funcional chama duas ferramentas e persiste as sessões MAF de supervisor e especialistas; retomada/restart real permanece pendente |
| Persistir supervisor e todos os specialists chamados | Duas tools chamadas salvam cada sessão; tool não chamada não causa gravação | Teste verifica duas sessões chamadas persistidas e especialista bound mas não invocado ausente do store |
| Corrigir resolução de agente usado/delegado | Resposta expõe nome de domínio do specialist e não nome técnico `AIFunction`; nenhum tool call mantém o supervisor como agente final | Teste confirma resposta consolidada, `AgentName` e `delegatedTo` com o nome do especialista |
| Preservar roteamento direto e contratos existentes | `targetAgent` bypassa supervisor; REST/SSE/SignalR mantêm argumentos, resposta final, owner e `sessionId` | Regressão da rota direta existente e revisão estática do frontend confirmam contrato inalterado; teste do supervisor cobre resposta direta sem especialista |
| Validar modelo e integração | testes focados, suíte Release; PostgreSQL com restart e isolamento entre tenants no Compose de validação | Build Release limpo; suíte completa com Compose: 735 aprovados, 1 teste vetorial explicitamente ignorado, 0 falhas. Regressão funcional após cobertura de provider error/cancelamento passou. Testes PostgreSQL validaram store global/listener e snapshot MAF ao recriar adapter; restart real do processo/host continua pendente |

## Pendências

Validar retomada/restart/isolamento real de sessão MAF em PostgreSQL usando exclusivamente o compose `tests/backend-validation/compose.yml` e ambas as variáveis de conexão protegidas por allowlist de host/porta/database/usuário. Manter commits separados do plano #120.

## Critérios de aceite

- [x] Prompt e tool registry representam exatamente os especialistas ativos com binding válido e suas versões atuais.
- [x] Rota inteligente executa supervisor MAF e pode chamar um ou vários agentes via `AIFunction`; seu retorno contém resposta útil e agente realmente delegado.
- [ ] Sessões do supervisor e especialistas persistem no store PostgreSQL com partições MAF tenant+usuário; owner errado, chave ausente e tenant cruzado falham sem vazamento.
- [x] Falhas na criação de binding não anunciam tools inexistentes; falhas durante execução não retornam resposta vazia com `Success=true`.
- [x] `targetAgent` explícito preserva execução direta; provider/model e selection do frontend não mudam de wire format.
- [ ] Testes de função cobrem escolha de especialista, resposta consolidada, multi-tool, ausência de candidato, erro/cancelamento, atualização de catálogo, restart e isolamento.

## Impacto no frontend para planejamento futuro

Nenhuma alteração frontend agora. O backend deve preservar `ChatHub.SendMessage(message,targetAgent,provider,model,apiKey,sessionId,selectedRoomId)`, request REST/SSE correspondente e `ReceiveMessage` com nome real do agent e `sessionId`. O UI `ChatConfigSidebar` chama vazio de Intelligent Router; `AgentChatPage` envia o agente da rota para execução direta. Futuro streaming passo a passo exigirá novo contrato versionado e alterações em `frontend/src/hooks/useChat.tsx`/renderização; hoje o hook limpa o estado visual após timeout fixo de 10s, então a revisão deve reportar essa limitação se respostas multi-agent ultrapassarem o prazo.

## Entrega e limites

Manter commits separados do upgrade MAF/Gateway. ADR aceita, implementação e validação são estados distintos. Documentar o modelo de sessões/seleção e sincronizar stories, índice e relatório. Sem alterar frontend, schema, fazer merge ou deploy neste plano.
