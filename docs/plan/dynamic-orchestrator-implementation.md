# Plano — Implementar o orquestrador supervisor dinâmico

Status: implementação funcional do supervisor dinâmico concluída; dois DI graphs/LLMManagers atualizaram via PostgreSQL NOTIFY e fizeram inferência Ollama. A API reabriu uma sessão MAF persistida após reinício real. Suíte: 753 aprovados, 1 skip vetorial, 0 falhas; build limpo. Evidência: [runtime](../backend/validation/maf-122-workflow-runtime-2026-09-29.md). Story: BACK-ORCH-122 · [ADR-038](../architecture/adr/038-dynamic-supervisor-orchestrator.md) · MAF/session-store 1.22 em [#120](maf-122-protocols-gateway.md).

## Objetivo

Fazer a rota inteligente do chat executar o `ChatClientAgent` supervisor com specialists do catálogo como tools MAF, mantendo seleção dinâmica por tenant, sessões, segurança e contratos atuais do chat.

## Estado encontrado

O supervisor já é executado diretamente pelo `ChatClientAgent`, com bindings válidos no prompt, sessões MAF separadas e cache de instruções fingerprintado. O `HierarchicalAgentFactory` agora mantém agentes customizados por tenant e atualiza a definição em cada resolução, evitando reuso de nome/configuração entre tenants.

## Etapas

| Entrega | Verificação | Estado |
|---|---|---|
| Separar os especialistas efetivamente bindados dos agentes ativos e usar apenas bindings no prompt/cache | Provider que falha ao criar tool não aparece como delegável; mudança de descrição/domínio/tier/tools muda fingerprint | Teste funcional confirma especialista indisponível omitido das tools MAF expostas; teste dedicado prova fingerprint muda por descrição/domínio/tools |
| Executar supervisor diretamente com a AgentSession tenant/user-scoped | Fake MAF/IChatClient chama ferramentas e retorna resposta; sessões supervisor/especialistas ficam serializadas | Regressão funcional chama duas tools e persiste sessões MAF do supervisor e specialists; PostgreSQL reabriu state com novos adapters/contextos e negou tenant cruzado. Após reinício real, a API reabriu a sessão do supervisor. |
| Persistir supervisor e todos os specialists chamados | Duas tools chamadas salvam cada sessão; tool não chamada não causa gravação | Teste verifica duas sessões chamadas persistidas e especialista bound mas não invocado ausente do store |
| Corrigir resolução de agente usado/delegado | Resposta expõe nome de domínio do specialist e não nome técnico `AIFunction`; nenhum tool call mantém o supervisor como agente final | Teste confirma resposta consolidada, `AgentName` e `delegatedTo` com o nome do especialista |
| Preservar roteamento direto e contratos existentes | `targetAgent` bypassa supervisor; REST/SSE/SignalR mantêm argumentos, resposta final, owner e `sessionId` | Regressão da rota direta existente e revisão estática do frontend confirmam contrato inalterado; teste do supervisor cobre resposta direta sem especialista |
| Validar modelo e integração | Build Release, suíte completa e PostgreSQL isolado para sessão/tenant | Build limpo; suíte 753 aprovados/1 skip; sessão MAF reaberta após reinício real da API; snapshot/version/hash, claims concorrentes, Wait após encerramento forçado e catálogo por tenant validados. Efeito externo interrompido não foi exercitado. |

## Pendências

Follow-up explícito: testar interrupção durante efeito externo e deduplicação pelo handler. Dois grafos independentes de DI/LLMManager/Gateway receberam reload PostgreSQL e inferência Ollama; esses managers rodaram no mesmo processo, portanto não demonstram propagação entre processos. Manter commits por contexto.

## Critérios de aceite

- [x] Prompt e tool registry representam exatamente os especialistas ativos com binding válido e suas versões atuais.
- [x] Rota inteligente executa supervisor MAF e pode chamar um ou vários agentes via `AIFunction`; seu retorno contém resposta útil e agente realmente delegado.
- [x] Sessões do supervisor e especialistas persistem no PostgreSQL por tenant+usuário; owner incorreto, chave ausente e tenant cruzado são negados. A sessão do supervisor foi reaberta por um novo processo da API.
- [x] Falhas na criação de binding não anunciam tools inexistentes; falhas durante execução não retornam resposta vazia com `Success=true`.
- [x] `targetAgent` explícito preserva execução direta; provider/model e selection do frontend não mudam de wire format.
- [x] Testes cobrem seleção, resposta consolidada, multi-tool, ausência de candidato, erro/cancelamento, atualização do catálogo e isolamento. A sessão do supervisor foi reaberta após reinício real; especialistas não tiveram prova equivalente em processo separado.

## Impacto no frontend para planejamento futuro

Nenhuma alteração frontend agora. O backend deve preservar `ChatHub.SendMessage(message,targetAgent,provider,model,apiKey,sessionId,selectedRoomId)`, request REST/SSE correspondente e `ReceiveMessage` com nome real do agent e `sessionId`. O UI `ChatConfigSidebar` chama vazio de Intelligent Router; `AgentChatPage` envia o agente da rota para execução direta. Futuro streaming passo a passo exigirá novo contrato versionado e alterações em `frontend/src/hooks/useChat.tsx`/renderização; hoje o hook limpa o estado visual após timeout fixo de 10s, então a revisão deve reportar essa limitação se respostas multi-agent ultrapassarem o prazo.

## Entrega e limites

Manter commits separados do upgrade MAF/Gateway. ADR aceita, implementação e validação são estados distintos. Documentar o modelo de sessões/seleção e sincronizar stories, índice e relatório. Sem alterar frontend, schema, fazer merge ou deploy neste plano.
