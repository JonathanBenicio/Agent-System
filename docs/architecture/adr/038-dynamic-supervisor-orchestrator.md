# ADR-038 — Orquestrador supervisor dinâmico com ferramentas MAF

Data: 2026-09-29 · Issue: [#122](https://github.com/JonathanBenicio/Agent-System/issues/122) · Story: BACK-ORCH-122 · [Plano](../../plan/dynamic-orchestrator-implementation.md). Dependência: MAF/session-store de [#120](https://github.com/JonathanBenicio/Agent-System/issues/120).

Decisão: aceita para implementação · Implementação: parcial em `40c262f`/`ebe8e9d` e worktree · Validação: build Release e suíte com Compose isolado: 738 aprovados, 1 teste vetorial explicitamente ignorado. Regressões locais de binding, multi-tool, metadados, resposta sem candidato/sem conteúdo, erro, cancelamento e persistência seletiva passaram. Testes PostgreSQL salvaram/reabriram snapshot MAF e workflow versionado; restart real do processo com sessões do supervisor/especialistas ainda pende.

## Contexto

O caminho inteligente já materializa um `ChatClientAgent` supervisor com especialistas publicados como `AIFunction`. Porém, o serviço ainda envolve a execução desse agente em um `WorkflowBuilder` de handoff que tenta converter os agentes do catálogo (`IAgent`) em `AIAgent`. As implementações do catálogo são abstrações de domínio, não `AIAgent`; assim, os executores de especialista não são ligados ao grafo e o resultado depende das tools do supervisor. Além disso, o serviço cria/carrega uma sessão MAF mas a execução pelo `InProcessExecution.RunAsync` não recebe essa sessão, e só persiste o supervisor, não as sessões dos especialistas chamados.

Isso ameaça as metas do produto: agentes criados/alterados dinamicamente não podem ser anunciados como disponíveis quando o binding não foi criado; prompt cache não pode ignorar mudanças de descrição/tools; sessão do supervisor e especialistas deve continuar owner/tenant-scoped.

## Decisão proposta

- Usar **supervisor com tools de especialistas** como único caminho inteligente nesta entrega. `OrchestratorHostBuilder` compõe um `ChatClientAgent` do MAF e especialistas materializados como `AIFunction`; `FrameworkOrchestratorService` executa o supervisor diretamente com a `AgentSession` carregada do `AgentSessionStore`.
- Construir as instruções com o conjunto exato de bindings criados com sucesso. Alteração de nome/descrição/domínio/tier/tools de agente ou tool auxiliar invalida a entrada de cache correspondente.
- Resolver as chamadas de função da resposta para o `AgentToolBinding` original e reportar o nome de domínio do especialista em `agentName`/`delegatedTo`, nunca o nome técnico da tool.
- Persistir após execução a sessão MAF do supervisor e de cada especialista efetivamente invocado. Todas as chaves incluem o partition `isolation` resolvido da identidade tenant+usuário; nenhum caminho inteligente deve criar chave sem contexto em produção.
- Preservar o caminho explícito `targetAgent` do produto: quando informado, o usuário escolheu execução direta e ela não passa pelo roteador inteligente.
- Manter catálogo/versionamento, ACL, tools permitidas, RAG, quotas e autorizações no domínio da aplicação. O MAF é o runtime de agentes/function calling; não recriar loop de tool call, AgentSession serialization ou WorkflowBuilder de um nó para simular handoff.

## Alternativas e trade-offs

- **Handoff/mesh MAF** exige especialistas expostos como `AIAgent` com identidade e configuração alinhadas ao catálogo; isso não é o contrato atual de `IAgent`, e mesh permite delegações laterais além da escolha do supervisor.
- **Supervisor com tools** já corresponde às tools geradas pelo catálogo e permite chamadas múltiplas pelo agente supervisor; conserva o controle de autorização e histórico atual e é compatível com configuração dinâmica por request.
- Usar `RoutePersistingRoutingChatClient` para substituir `ContextAwareChatClient` foi descartado nesta proposta: seu escopo é persistir a rota de cliente na AgentSession, mas o app ainda precisa escolher credenciais por tenant/BYOK, fallback e aplicação das quotas/auditoria por tenant.

## Consequências e migração

A mudança é interna ao backend e não requer schema. O frontend já envia `targetAgent` opcional, provider/model e `sessionId`; a rota vazia continua significando roteamento inteligente, e a rota `/chat/:agentName` continua significando execução direta. `ChatHub`/REST devem continuar emitindo a resposta final com o nome real do agente usado e o mesmo `sessionId`.

Não adicionar contrato de passos/token streaming neste plano. A UI atual consome `ProcessingStarted`, `StreamEvent` e `ReceiveMessage`; uma futura visualização de supervisor/especialistas exigirá desenho separado de eventos e estados do frontend. A revisão encontrou timeout visual de 10s em `frontend/src/hooks/useChat.tsx`; ele pode encerrar o spinner antes de uma resposta longa, mas não será alterado enquanto o escopo continuar backend-only.

## Critérios de verificação

`ChatClientAgent.RunAsync` deve escolher e invocar um especialista disponível e incorporar a resposta; sessões supervisor/especialista devem retomar após restart e não atravessar tenant/usuário. Especialista sem binding não é anunciado no prompt. Múltiplas tools chamadas devem persistir todos os especialistas invocados. Sem tools aplicáveis, supervisor pode responder diretamente. Chamada com `targetAgent` continua execução direta. Erro de provider/quota/cancelamento mantém o contrato de erro atual e não declara sucesso vazio.
