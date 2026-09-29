# ADR-039 — Configurações efetivas para chat e sessões

Data: 2026-09-29 · Issue: [#123](https://github.com/JonathanBenicio/Agent-System/issues/123) · Story: BACK-CHAT-123 · [Plano](../../plan/chat-session-user-settings.md).

Decisão: aceita para implementação; validação de ponta a ponta pendente.

## Contexto

O frontend envia mensagens por SignalR ou REST. O fallback REST lê campos de resposta que não pertencem a `AgentResponse` e não atualiza `sessionId`. A seleção de modelo vive em `localStorage`; `llmApi.configuration()` usa uma rota reservada ao Platform Admin. O teste da chave BYOK retorna sucesso por existência, enquanto POST/PUT devolvem o segredo. Skills do tenant podem ser criadas, porém não há estado ativo/desativo aplicado ao prompt. Esses fatos impedem que “salvo” signifique “usado”.

## Decisão

- Manter identidade de usuário e tenant derivada do principal autenticado e do middleware. Preferências de chat são por `(tenantId, userId)`; chaves BYOK e catálogo/ativação de skills são do tenant.
- Qualquer membro ativo pode ler catálogo disponível, manter sua preferência de provider/modelo, conversar e gerir somente suas sessões. Owner/Admin do tenant pode gerir BYOK e habilitar/desabilitar skills; Viewer/Operator não recebe direito de escrita por estar autenticado.
- Expor catálogo seguro de providers/modelos ao usuário sem conceder `LLMController` de administração global. Seleções só podem referenciar provider/modelo efetivamente disponíveis. A preferência salva é fallback para a próxima chamada; valor explícito por request prevalece quando permitido.
- Credencial BYOK é cifrada, nunca incluída em DTO de saída, inclusive após POST/PUT. “Testar chave” deve chamar a validação real do provider e informar falha real. O runtime resolve apenas chave do tenant ativo e provider selecionado.
- Skill possui estado de ativação persistido por tenant. A leitura distingue disponível de ativa, e o provider de contexto MAF inclui somente skills ativas relevantes. Ferramentas continuam limitadas ao catálogo/autorização do agente; o estado da skill não concede novas permissões.
- Sessão tem identidade criada pelo servidor, owner/tenant imutáveis e histórico persistido. Abrir e retomar reutilizam a sessão; encerrar altera estado sem apagar histórico, enquanto exclusão é ação separada. REST e SignalR usam o mesmo contrato de resposta visível ao usuário.
- Administração global de providers permanece em #120; A2A/AG-UI preview em #121.

## Alternativas e consequências

`localStorage` pode manter uma seleção visual temporária, mas não é fonte de verdade por usuário/tenant. Reusar a rota de Platform Admin na UI ampliaria privilégios. O schema precisará de persistência de preferência e ativação de skill; migrations serão aplicadas somente ao PostgreSQL do Compose isolado. A validação precisa cobrir salvar, reiniciar/retomar e observar a seleção/skill/chave na execução do chat, além do contrato da tela.
