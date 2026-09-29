# Especificações das issues abertas — snapshot 2026-09-29

Status: fichas das 46 issues elaboradas e vinculadas; checagem documental aprovada; [PR documental #125](https://github.com/JonathanBenicio/Agent-System/pull/125) aberto como draft · Branch: `docs/open-issue-specifications`.

## Objetivo, escopo e regra de atualização

Esta página é o registro canônico das especificações das issues abertas consultadas na API do GitHub em 2026-09-29. O snapshot tinha **46 issues abertas e 5 PRs abertos**. A contagem é temporal: issues abertas depois desta data entram em uma próxima revisão, não retroativamente neste inventário.

Para evitar cópias divergentes, cada cartão abaixo aponta ao ADR, story, plano e evidência existentes quando eles continuam válidos. Quando faltarem, o cartão contém a especificação mínima proposta, critérios verificáveis, dependências e decisões ainda sem dono. ADRs aceitos não são prova de implementação; código e validações têm estado próprio.

Issues antigas que parecem concluídas, duplicadas ou substituídas **continuam abertas**. O cartão registra evidência e recomendação; o responsável pode fechar, reabrir ou dividir a issue após a revisão. Nenhum comportamento de produto, decisão de arquitetura ou gate de CI é considerado aprovado só por aparecer aqui como proposta.

## Plano de trabalho

| Etapa | Entrega | Verificação | Estado |
|---|---|---|---|
| 1 | Conferir as 46 issues, 5 PRs, estado local, código e documentação canônica | API GitHub; árvore de trabalho; referências no código/docs | Concluída para o snapshot |
| 2 | Dar a cada issue uma ficha com problema, escopo, aceite, dependências, fonte canônica e disposição recomendada | 46 âncoras verificadas no registro; gaps e duplicatas marcados | Concluída |
| 3 | Corrigir especificações desatualizadas e criar apenas artefatos realmente ausentes | ADR/story/plano existentes reutilizados; decisões incertas marcadas como pendentes | Concluída como especificação; sem implementar decisões pendentes |
| 4 | Adicionar link para a ficha em cada uma das 46 issues sem fechar issues | Leitura posterior da API confirmou 46/46 links e bodies preservados | Concluída |
| 5 | Sincronizar índices, validar links e publicar commits/PR documental separado | Checker: 160 arquivos, 799 links, 0 quebrados; PR #125 draft separado, base `feat/chat-session-user-settings` | Enviado; revisão/merge pendentes |

## Resumo do estado atual

| Classe | Issues | Tratamento desta especificação |
|---|---|---|
| Trabalho recente com pacote canônico | #110–#123 | Reutilizar ADRs/stories/planos; explicitar evidência pendente e relação entre PRs/issues. |
| Segurança/tenancy ainda em PR draft | #111–#117 | O PR #119 permanece draft; não fechar issues até revisão e gates descritos no relatório. |
| Pedido obsoleto ou provável duplicata | #12–#16, #34, #45, #60, #62–#66, #74–#80, #90, #92–#93, #96–#99, #104–#106 | Corrigir baseline, mapear código atual e indicar `concluída`, `parcial`, `duplicada`, `substituída` ou `sem decisão`; recomendar destino sem fechar. |
| Roadmap sem critérios por entrega | #83–#88 | No snapshot #88 e #83–#87 estão abertas. O roadmap não altera o gate de cobertura e deve separar comportamento de cobertura. |
| Visão estratégica sem execução delimitada | #109 | Converter a visão extensa em backlog priorizado com objetivos observáveis, dependências e propostas claramente distintas de decisão aceita. |

> **Correção de contagem:** a seleção do usuário mencionou “40”; a leitura atual da API retornou 46 issues. O escopo segue a instrução original de cobrir todas as abertas, portanto este trabalho usa 46.

## Issues recentes e em cadeia

## issue-123

**[BACKEND/FRONTEND] Chat, sessões e configurações efetivamente usadas**

**Classificação:** especificação completa e aceite funcional cumprido; entrega ainda em PR draft.

**Resultado esperado:** preferências provider/modelo persistidas por `(tenantId,userId)` governam a próxima chamada; credencial BYOK validada pelo provider não volta em resposta; skills ativas entram no contexto sem conceder ACL de tools; sessão do dono preserva histórico e não cruza tenant/usuário.

**Aceite verificável:** salvar e ler configuração; inspecionar provider local e confirmar modelo/chave efetivos; retomar mesma sessão e contexto após reload; negar outro dono/tenant; encerrar sem apagar histórico; confirmar que skill desligada não entra em sessão nova; respostas nunca contêm segredo.

**Fontes:** [ADR-039](../architecture/adr/039-chat-session-user-tenant-settings.md), [BACK-CHAT-123](../USER-STORIES.md#back-chat-123--chat-sessoes-e-configuracoes-efetivamente-usadas), [plano](chat-session-user-settings.md), [contrato](../backend/chat-sessions-settings.md), [validação](../backend/validation/chat-session-settings-2026-09-29.md), [PR #124](https://github.com/JonathanBenicio/Agent-System/pull/124).

**Pendência real:** PR #124 ainda draft devido ao lint global do frontend (22 erros + 1 aviso fora dos arquivos desta entrega). O review recebido nesta sessão também apontou P2 na regra staged `.agents/` do `.gitignore`; essa regra está apenas no índice local e não consta no head publicado de #124. Tratar como alteração local separada, estreitar o padrão e preservar o staging antes de qualquer commit. Manter issue aberta até revisão do gate.

## issue-122

**Supervisor dinâmico com MAF**

**Classificação:** pacote de especificação existente; distinguir decisão, implementação e validação; issue não deve ficar sem PR/componente de entrega associado.

**Resultado esperado:** supervisor MAF 1.22 usa apenas especialistas ativos com binding válido; prompt/fingerprint refletem catálogo atual; sessão do supervisor e somente dos especialistas invocados é persistida por tenant/usuário; seleção direta de `targetAgent` mantém contrato atual.

**Aceite:** seleção e delegação; especialista desativado/sem binding ausente das opções; alteração de descrição/capabilities refletida sem restart; persistência/retomada do supervisor e especialistas; negação cross-tenant; erro/quota/cancelamento não vira resposta de sucesso vazia.

**Fontes:** [ADR-038](../architecture/adr/038-dynamic-supervisor-orchestrator.md), story [BACK-ORCH-122](../USER-STORIES.md#back-orch-122--orquestrar-agentes-dinamicos-pelo-supervisor-maf), [plano](dynamic-orchestrator-implementation.md), [#120](https://github.com/JonathanBenicio/Agent-System/issues/120), [#121](https://github.com/JonathanBenicio/Agent-System/issues/121).

**Lacuna de rastreabilidade:** o corpo da issue ainda diz “PR/commits a vincular”; determinar, pelo diff/commit list de #119, se esta implementação está incluída. Atualizar o link da issue para o PR correto. Não dizer que crash no meio de efeito externo é exactly-once; handlers precisam deduplicar ou compensar.

## issue-121

**Validar A2A e AG-UI em hosting preview**

**Classificação:** especificação adequada e explicitamente despriorizada; implementação/E2E ausente.

**Resultado esperado:** sob feature flags e com versões preview, validar autenticação, tenant/membership, sessão, streaming e cancelamento sem divulgar estabilidade de contrato que o pacote não oferece.

**Aceite:** restore/build/host; chamada autenticada com fixture local; ausência de auth, tenant inválido/inativo e membership negada; sessão/stream A não aparece em B; cancelamento libera recursos. Registrar versão exata dos pacotes e o ambiente.

**Fontes:** [ADR-037](../architecture/adr/037-a2a-agui-preview-validation.md), story [BACK-PROTO-121](../USER-STORIES.md#back-proto-121--validar-a2a-e-ag-ui-sob-hosting-preview), [plano](a2a-agui-preview-validation.md), dependência #120 e critério residual de #117.

**Decisão proposta:** manter prioridade baixa enquanto os hosts estiverem preview; se a validação não for requisito para release atual, registrar como follow-up e retirar o critério duplicado de #117 só depois de revisão humana. Nenhum E2E pode ser alegado enquanto o host estiver desativado.

## issue-120

**Atualizar MAF e integrar providers ao Gateway**

**Classificação:** pacote de especificação existente; corpo e evidência pública estão defasados em relação aos relatórios mais recentes.

**Resultado esperado:** alinhar família de pacotes MAF; sessão preserva identidade/serialização; Gateway registra providers globais de produção; BYOK mantém escopo tenant; configuração global cifrada e auditável; quotas de tenant continuam como autoridade.

**Aceite mantido:** compatibilidade documentada antes do bump; build/testes; criação/serialização/retomada/isolation de sessão; provider habilitado e desabilitado no Gateway; chamadas de provider global e streaming; bypasses por BYOK e config legada; migration/auditoria segura; erros e cancelamento.

**Fontes:** [ADR-036](../architecture/adr/036-maf-122-protocols-and-gateway.md), story BACK-MAF-120, [compatibilidade](maf-122-compatibility-review.md), [plano](maf-122-protocols-gateway.md), [evidência](../backend/validation/maf-122-workflow-runtime-2026-09-29.md), PR #119.

**Lacunas a alinhar antes de fechar:** contagens publicadas não coincidem entre corpo da issue, story, PR e relatório; serviço Gateway fixture não prova chamada de LLM de produção nem atualização de LLMManager entre hosts; restart real em efeito externo, provider real, semântica retry/idempotência, cancelamento longo, atualização do hook frontend e A2A/AG-UI E2E permanecem explicitamente pendentes. Atualizar a issue com resultados e limites atuais; manter os itens de workflow dinâmico vinculados a #122.

## issue-117

**Membership, papéis, suporte e limites**

**Classificação:** especificação existente; 4 de 5 critérios exercitados, 1 permanece protocolo-preview.

**Resultado esperado:** papel de plataforma separado do papel tenant; backfill conserva papel; suporte é grant temporário/auditável/revogável e ACL de conteúdo continua obrigatória; plano é teto e quota pode restringir.

**Aceite funcional:** papel independente em dois tenants; Platform Admin sem conteúdo implícito; grant expirado/revogado nega; quotas aplicadas em recursos e custos; migration/backfill testados. Critério A2A/AG-UI só fecha com evidência de #121.

**Fontes:** [ADR-035](../architecture/adr/035-backend-core-isolation-and-reliability.md), story BACK-FIX-111–117, [plano](backend-core-remediation.md), [relatório](../backend/validation/backend-core-remediation.md), PR #119, #121.

**Limites que pertencem à especificação:** bytes são origem lógica, não espaço físico; `MaxMonthlyBudgetUsd` é projeção diária × 30; fixture de backfill não é base real; RPM é processo-local. Não converter esses limites em “validado para produção”.

## issue-116

**Seeding de skills por tenant**

**Classificação:** critérios declarados implementados nos cenários exercitados; issue aguarda revisão do PR #119.

**Aceite:** IDs e uniqueness por tenant; seeding idempotente/concorrrente; quatro defaults completos; IDs/customizações preservados em restart.

**Fontes:** ADR-035, BACK-FIX-116, `docs/plan/backend-core-remediation.md`, relatório de validação e PR #119.

**Disposição recomendada:** não criar novo plano; corrigir contagem/branch de evidência se stale e fechar após review/merge do PR que realmente contém o fix.

## issue-115

**Concorrência de quotas**

**Classificação:** corrigida para cenários medidos; limites de multi-host explicitamente abertos; revisão pendente em #119.

**Aceite:** incrementos confirmados não duplicam nem desaparecem sob context/factory concorrentes; reset UTC; tokens/custo bloqueiam REST/OpenAI-compatible e SSE após restart.

**Fontes:** ADR-035, BACK-FIX-115, plano/relatório backend-core, PR #119.

**Limite:** RPM do limiter é in-process; o teste em dois repositórios não prova concorrência entre dois processos/hosts. Recomendar issue follow-up somente se rate limiting distribuído for requisito agora; não ampliar critérios atuais em silêncio.

## issue-114

**Isolamento da SessionIsolationKey do MAF**

**Classificação:** fluxo funcional validado, incluindo restart da API; revisão de PR pendente.

**Aceite:** REST/SSE/SignalR geram `sessionId` e resposta; outro owner/tenant recebe negação sem histórico; após restart, mesmo ID, mensagens e estado serializado são retomados.

**Fontes:** ADR-035, BACK-FIX-114, plano/relatório backend-core, PR #119.

**Disposição recomendada:** manter como issue de entrega/revisão; nenhuma nova feature spec é necessária. Distinguir restart API real de restart de host em produção multi-node.

## issue-113

**RAG/ACL de salas e filtro SQL**

**Classificação:** critérios funcionais exercitados em PostgreSQL e chat real; revisão pendente.

**Aceite:** filtro vazio falha fechado; query usa coluna existente e filtra antes do scoring; conteúdo de sala permitida é recuperado; conteúdo de outra sala/tenant é negado; ingestão e resposta citam o chunk autorizado.

**Fontes:** ADR-035, BACK-FIX-113, plano/relatório backend-core, contratos `docs/backend/access-tenants.md` e RAG, PR #119.

**Limite:** dados/frases são sintéticos; não alegar corpus de produção. Fechar depois da revisão do PR e atualização da evidência.

## issue-112

**Tenant no handshake e eventos SignalR**

**Classificação:** implementação e cenários negativos dos hubs reportados; revisão pendente.

**Aceite:** claim/header e membership obedecem a uma regra sem override divergente; desconhecido/inativo nega; API key Viewer permanece Viewer; eventos de cinco hubs não cruzam tenant; rota Gateway global requer Platform Admin.

**Fontes:** ADR-035, BACK-FIX-112, `docs/backend/access-tenants.md`, `docs/backend/transports.md`, relatório backend-core, PR #119.

**Lacuna de terminologia:** separar seleção de tenant pelo principal autenticado de `X-Tenant-Id`; query string de browser WebSocket não deve ser apresentada como mecanismo que autoriza tenant. Documentar a diferença entre REST, SignalR e API-key subject.

## issue-111

**API key Viewer e override de tenant**

**Classificação:** corrigida nos cenários reportados; revisão de PR pendente.

**Aceite:** key sem membership/divergente nega; papel persistido é respeitado; nenhum backfill promove Platform Admin; HTTP e handshake SignalR negam troca de tenant; migration conserva papel.

**Fontes:** ADR-035, BACK-FIX-111, contratos de auth/tenant, relatório backend-core, PR #119.

**Disposição recomendada:** sem novo pacote de arquitetura; fechar só após revisão/merge e atualização dos resultados públicos.

## issue-110

**Contratos do backend e diagnóstico do núcleo**

**Classificação:** pacote documental já produzido; PR #118 segue draft e o corpo da issue preserva resultados mistos históricos.

**Resultado esperado:** docs canônicos refletem código/contratos e distinguem baseline histórico, validação posterior, decisões futuras e estabilidade não comprovada.

**Aceite:** contrato de endpoints/DTOs, acesso/tenant, resources, transportes, links, evidências, templates e gaps rastreáveis; cada resultado informa branch/SHA/ambiente.

**Fontes:** ADR-034, BACK-DOC-001, [plano](backend-documentation-validation.md), [hub backend](../backend/README.md), [diagnóstico](../backend/validation/2026-09-28.md), [PR #118](https://github.com/JonathanBenicio/Agent-System/pull/118).

**Lacuna:** a issue deve tratar os relatórios f8de7a6/PR #118 e remediação #119 como snapshots diferentes; não escrever contagem de testes atual dentro de resultado histórico. PR #118 draft documenta 22,39% de cobertura e gaps funcionais; manter isso explícito até o gate acordado.

## issue-109

**Roteiro “10/10” de arquitetura e prontidão**

**Classificação:** não é issue implementável como está; corpo de 42 KB é visão/roadmap, sem baseline atual, owner, limite de escopo ou DoD por entrega.

**Especificação proposta para esta issue:** objetivo é manter um mapa de capacidades e dependências; cada iniciativa executável deve virar issue própria com baseline no código, outcome observável, prioridade justificável, ADR quando decisão nova, story/BDD, plano e critérios mensuráveis. “10/10” ou nota subjetiva não é critério de aceite.

**Não incluir na issue mestre:** autorizar implementação de todos os itens, migrar frameworks, adotar deploy, rebaixar gates ou assumir que lista aspiracional corresponde ao produto atual.

**Aceite proposto:** visão classificada em atual/validar/futuro/duplicado; itens ativos apontam para issues específicas; dependências e exclusões claras; nenhum item sem dono/resultado fica implícito como aprovado.

**Fontes:** issue #109; [master roadmap](master-roadmap-2026.md); [overengineering assessment](../planejamento/overengineering-assessment.md). Mapear duplicações com #12–#16, #74–#80, #88, #104–#106 e #120–#123. A disposition recomendada é converter para roadmap/índice e não executar como feature única; decisão de fechamento fica com o usuário.

## issue-106

**FIDES / proteção de dados sensíveis**

**Classificação:** descrição atual está objetivamente desatualizada; `FidesDataProtectionMiddleware` existe e é registrado no pipeline.

**Estado de código observado:** regras são regex estáticas para CPF formatado, cartão, e-mail e alguns formatos de token; entram mensagens User/System; o accessor do tenant é usado no log. Isso não comprova regras configuráveis por tenant, classificação completa, auditoria segura, cobertura de formatos ou política para imagens/anexos.

**Especificação pendente:** declarar quais dados e formatos são cobertos; se as regras são globais ou configuráveis por tenant; quando rejeitar versus mascarar; garantir que segredo original não apareça em log/trace/erro; definir observabilidade e performance; testar positivos e falsos positivos sem dados reais; definir se mensagens de sistema são mascaradas.

**Aceite mínimo proposto:** entrada com CPF/cartão/email/token cobertos é mascarada antes do primeiro provider; outras roles/media têm comportamento explícito; tenant A não lê regras/config de B; logs não contêm original; falha do detector tem modo seguro documentado; corpus sintético e casos negativos cobrem false positive.

**Fontes:** ADR-005 e ADR-027 hoje contêm afirmações que excedem a implementação; [plano histórico](maf-complete-migration-plan.md) também diz que não existe. Atualizar os três estados para uma única descrição vigente antes de tratar a issue como concluída.

## issue-105

**Hyperlight / sandbox real**

**Classificação:** implementação atual é simulada; o executor produz saída fixa e métricas fixas, apesar da descrição/log sugerir execução WASM real. A afirmação antiga do ADR-006 de que nenhum pacote .NET público existe ficou desatualizada.

**Resultado seguro esperado:** nenhuma ferramenta ou UI deve representar esta simulação como isolamento real; o caminho simulado precisa estar marcado como Lab e não pode processar código confiável/segredos como se fosse sandbox. Pesquisa de disponibilidade do runtime oficial deve ser datada e usar fonte primária; disponibilidade de package não prova compatibilidade com .NET/Windows/Linux nem manutenção.

**Aceite para manter simulação:** flag/registro de capacidade em modo `Simulated`; resposta e log identificam explicitamente simulação; documentação proíbe uso como boundary de segurança; caminho desativável sem execução de output fixo como resultado real; teste demonstra que payload arbitrário não é executado.

**Aceite para futuro runtime real:** threat model; idiomas/ABI suportados; limites de CPU/memória/tempo, filesystem/rede/syscalls, cancelamento e teardown; escaping de stdout/stderr; isolamento entre tenants; versão pinada; prova negativa de acesso ao host; fallback fechado se runtime ausente. O pacote oficial `Microsoft.Agents.AI.Hyperlight` foi publicado em versões preview; a versão NuGet consultada em 2026-09-29 é `1.21.0-preview.260911.1`, com dependência de `Microsoft.Agents.AI.Abstractions >=1.21.0`, `Microsoft.Extensions.AI.Abstractions >=10.10.0` e `Hyperlight.HyperlightSandbox.Api >=0.6.0`; o SDK sandbox tem versão `0.7.0`. Ambos permanecem preview e sua compatibilidade com MAF 1.22/runtime/plataformas deste produto ainda não foi testada. Fontes primárias: [pacote do Agent Framework](https://www.nuget.org/packages/Microsoft.Agents.AI.Hyperlight/) · [README .NET oficial](https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Hyperlight/README.md) · [API do sandbox](https://www.nuget.org/packages/Hyperlight.HyperlightSandbox.Api/).

**Fontes:** `HyperlightSandboxedExecutor.cs`, ADR-006 (decide manter stub, mas logs/tool description contradizem), ADR-027 e plano MAF histórico. A issue atual não pode ser concluída só por “package não disponível”: a pesquisa atual comprova pacote Preview público, não runtime estável; atualizar o critério para compatibilidade, threat model e decisão de ativação segura.

## issue-104

**PowerFx em manifests**

**Classificação:** descrição está desatualizada/parcial: `AgentYamlValidator` usa `RecalcEngine.Check`, então parser/compilação sintática existe; isso não demonstra que expressions sejam avaliadas em runtime ou que functions/context/timeouts estejam limitados.

**Especificação pendente:** separar validação de sintaxe de execução; decidir funções permitidas e variáveis de entrada; definir comportamento de erro/null/type; evitar acesso a dados/secrets de outro tenant; definir cancelamento/limite de avaliação; preservar manifests existentes e documentar versão de `Microsoft.PowerFx`.

**Aceite proposto:** expressões inválidas falham com diagnóstico estável; expressões válidas passam validação; se runtime avaliar regra, somente funções e tipos aprovados ficam disponíveis; timeout/cancelamento são testados sem bloquear chat; nenhum objeto service/tenant completo chega ao motor; compatibility fixtures verificam manifests atuais.

**Fontes:** código em `AgentYamlValidator.cs`; ADR-005 registra integração, ADR-027 e `maf-complete-migration-plan.md` ainda descrevem um estado antigo. Não promover a afirmação “execução PowerFx segura” até localizar/validar o ponto de runtime.

## Issues legadas de auto-bootstrap/tenant — #99, #98, #97 e #96

## issue-99

**SystemBootstrapService**

**Classificação:** classe/serviço existe; o issue descreve criação automática do primeiro tenant e credencial inicial.

**Aceite que precisa de confirmação por código/teste:** banco vazio bootstrap uma única vez; banco existente nunca recria tenant/key; configuração ausente/inválida falha sem criar credencial conhecida; não gerar segredo padrão nem colocar key em log; startup concorrente é idempotente; admin platform e membership tenant não se confundem.

**Fonte:** `SystemBootstrapService`, ADR-026, story de auth em `USER-STORIES.md`. A issue propõe SHA-256 e `AdminApiKey` estática; atualizar segurança contra baixa entropia/legacy antes de tratar como prescrição vigente. O serviço também semeia `VisionAnalyst` e `EditorChefe` com instruções específicas de banner/imóveis no tenant `admin`; especificar se isso é demo/lab ou comportamento de produto, e manter seeds de produto fora do bootstrap genérico.

## issue-98

**API key handler e TenantMiddleware**

**Classificação:** componentes existem; muitos requisitos se sobrepõem a #111/#112/#117 e têm implementação/contratos atualizados em ADR-035.

**Aceite refinado:** hash da credencial validado por mecanismo atual; subject/tenant/role vêm de vínculo confiável; tenant divergente/desconhecido/inativo é negado por rota protegida; seleção no SignalR não amplia papel; endpoints públicos/isentos têm exceção explicitamente documentada.

**Fonte:** `ApiKeyAuthenticationHandler`, `TenantMiddleware`, testes multi-tenancy, ADR-026/035. Marcar como duplicada/substituída por #111–#117 e recomendar fechamento depois da revisão de #119.

## issue-97

**Remover tenant default hardcoded**

**Classificação:** especificação histórica de “expurgo total”; `default` pode continuar em fixture/config legado, mas não deve ser confundido com fallback de autorização.

**Aceite refinado:** nenhum fluxo autenticado obtém tenant por fallback silencioso; dado legado só é migrado com tenant/role explícitos; entidades tenant-scoped inválidas falham; exceções dev/test ficam identificadas e isoladas; busca usa somente `src/tests` normativos, não cópias/histórico. A varredura atual ainda encontra `TenantId ?? "default"` em `DynamicOnnxProcessorTool`, `WebhooksController`, `ExternalQuotaSyncService`, `PostgresToolManager` e `PostgresSkillManager`; é preciso decidir e testar caso a caso se vira negação, contexto explícito ou compatibilidade apenas em migração/test fixture. Essa pendência impede marcar a especificação como atendida.

**Fonte:** ADR-026, middleware/DBContext atual e testes. A frase “nenhuma ocorrência da palavra default” é critério excessivo e não mede isolamento; substituir por buscas comportamentais e referências de runtime.

## issue-96

**AccessApiKeyEntity e remoção de fallback no DbContext**

**Classificação:** a entidade, persistência e handler associados existem; issue tem nomes/critério legado e overlap com #98.

**Aceite refinado:** schema atual tem unicidade/índice de hash e tenant; segredo sai uma vez em criação e não é retornado em leitura; `SaveChanges` não inventa tenant; queries/autorização respeitam membership; migrations idempotentes e backfill rastreável.

**Fonte:** ADR-026, `AgenticDbContext`, entidade atual, migration, testes. Verificar naming no código antes de preservar `AccessApiKeyEntity` como requisito literal.

## Issues de chat e integração antiga

## issue-93

**Epic de chat/sessões/salas/workflows**

**Classificação:** epic muito amplo e parcialmente sobreposto a #92 e #123; possui subtarefas históricas com evidência de conclusão.

**Especificação:** fechar cada subcapacidade conforme issue própria, não usar este epic para novos comportamentos; itens ativos restantes precisam estar enumerados com owner, contrato, estado de UI/backend, acceptance e dependência. Sessões de #123 têm escopo separado; workflows/MCP/RAG continuam seus próprios contratos.

**Fonte:** `docs/issue-chat-enhancements.md`, `plan/completed/chat-enhancements.md`, story `us-advanced-chat-session-management.md`, #92/#123. Recomendação: classificar como duplicada/encerrável após reconciliar subtarefas e PRs.

## issue-92

**Chat avançado: sessões, knowledge rooms, agentes, workflows**

**Classificação:** duplicada em grande parte por #93; issue longa com alvos amplos e expectativas de UI/backend misturadas.

**Especificação restante:** comparar cada requisito com `USER-STORIES.md` e contratos atuais; marcar como entregue com link/evidência, ainda pendente com acceptance reproduzível, ou substituído por issue focal. Não adicionar nova UI/chat wire format sem story específica.

**Fontes:** issue #92, `us-advanced-chat-session-management.md`, docs backend/README, planos concluídos e #123. Recomendar consolidar num epic único e fechar duplicata somente após os owners confirmarem.

## issue-90

**Ambiguidade DI do MetaAgentOrchestrator**

**Classificação:** issue já descreve solução e build/tests concluídos; sem critérios abertos na descrição.

**Aceite suficiente:** startup DI resolve uma única composição; solução não muda o comportamento suportado; regressão/unitários passam.

**Lacuna:** body não cita commit/PR nem evidência atual, e solução mencionada com dois constructors pode ter sido substituída. Atualizar evidência com estado atual e recomendar fechamento se a correção está no ramo publicado.

**Pacote documental:** ADR/story/plano não necessários para bugfix isolado; N/A justificado. Manter a issue como fonte do problema/aceite e anexar commit/PR e teste que confirma a composição DI atual.

## Issues do roadmap de teste — #88 e #83–#87

**Regra compartilhada:** privilegiar fluxos críticos e integrações que comprovem semântica real. O limite de cobertura existente no CI continua separado e não deve ser apresentado como objetivo principal do teste funcional. Usar o PostgreSQL do Compose do projeto/porta isolada, conforme orientação do usuário; `UseInMemoryDatabase` não valida SQL, migration, concorrência PostgreSQL ou query filters relacionais.

## issue-88

**Epic de testes .NET**

**Classificação:** roadmap existe; 80% global aparece como meta primária, enquanto a medição disponível é histórica e abaixo do gate.

**Aceite revisado:** inventário de riscos/fluxos e ownership; fases vinculadas a issues testáveis; resultados separados por unit, integration, E2E, carga e security; cobertura comunicada como gate CI com relatório datado, não como proxy de comportamento correto.

**Fonte:** [backend-testing-roadmap.md](backend-testing-roadmap.md); issues #83–#87. Revisar instruções conflitantes sobre emulador vs PostgreSQL real.

## issue-87

**Carga e CI**

**Especificação:** perfis k6 com workloads, concorrência, duração, dataset, thresholds e ambiente identificados; cobertura/tool report não substitui SLO de throughput/latência; Gateway de carga deve ter service fixture claramente distinto de provider real; pipeline falha conforme gates configurados.

**Fonte:** #88, `tests/k6`, workflows, docs/plan/ci-pipelines.md. Definir SLO numérico somente após baseline, não usar “perfeitamente”.

## issue-86

**Hubs e tempo real**

**Especificação:** testar cada hub/action/event listado no inventário; tenant/membership negativos; recuperação/reconnect; cancelamento/cleanup; comparar grupos de A/B; separar A2A/AG-UI em #121 por serem preview.

**Fonte:** #112/#114 e `docs/backend/transports.md`. Evitar duplicar a mesma matriz sem contrato/event ID.

## issue-85

**API E2E/WebApplicationFactory**

**Especificação:** usar test host para autenticação, middlewares, autorização, contrato HTTP, correlação e mocks de provider; persistência/query SQL fica na integração Compose de #84. Cada endpoint testado informa request, status, body e fixture auth; não exigir só 200/401/400.

**Fonte:** #110, `docs/backend/api-core.md`, `endpoint-inventory.md`, validation harness existente.

## issue-84

**Persistência e isolamento tenant**

**Especificação:** testes sobre PostgreSQL real no Compose isolado para migrations, SQL específico, stores, concorrência e query filters; fixtures namespaced; assegurar limpeza por IDs sintéticos; testar A lê/escreve seu dado e não lê/altera B; não usar InMemory como prova de SQL/isolamento PostgreSQL.

**Fonte:** #111–117, `tests/backend-validation/compose.yml`, backend-core remediation.

## issue-83

**Cobertura unitária estrutural**

**Especificação:** priorizar casos de resultado correto, erro, limites e concorrência em ferramenta ONNX, reranker e fila; manter unitários sem dependências externas. “>80% nas classes core” e “suite inteira <10s” necessitam alvo, baseline e definição de métrica; não substituir validação funcional pelo percentual.

**Fonte:** #88, teste atual `DynamicOnnxProcessorToolTests`, teste reranker/queue. O usuário já definiu que cobertura não é prioridade do produto; o gate institucional permanece sem alteração.

## Issues ONNX e integração de capacidades

## issue-80

**Melhorias de ONNX (.data, aspecto e aceleração)**

**Classificação:** descrição não contém acceptance; overlaps com #74 e US-45–47, e vários itens parecem implementados.

**Aceite a confirmar no código:** modelo split e múltiplos arquivos persistem/recarregam; resize/padding/crop mantém dimensão esperada; providers DirectML/CUDA falham para CPU sem corromper sessão; formato/bytes/limites validados; isolamento de diretório por tenant e path traversal negado; testes verificam saída, não apenas construção de tensor.

**Fonte:** [US-45–47](../USER-STORIES.md#épico-10-dynamic-onnx-in-process-inference-engine-roadmap-q2-2026), [ADR-010](../architecture/adr/010-onnx-runtime-in-process.md), plano concluído `onnx-in-process.md`, #74.

## issue-78

**Tenant no handshake SignalR do frontend**

**Classificação:** requisito do issue pode conflitar com autoridade de tenant: adicionar header do workspace não pode permitir trocar tenant sem membership/claim.

**Aceite revisado:** determinar o tenant pelo principal e seleção autorizada do workspace; browser transports recebem só mecanismos suportados; reconexões preservam o mesmo tenant autorizado; mudança de workspace abre conexão nova e valida membership; nenhum evento vaza para workspace anterior; API key não é promovida nem obtém tenant arbitrário por header/query.

**Fonte:** `frontend/src/lib/signalr*`, `docs/backend/access-tenants.md`, #112. Retirar prescrição “importar dinamicamente store” se configuração atual não precisa disso; acceptance comportamental é fonte.

## issue-76

**Cache/pool ONNX**

**Classificação:** classe `OnnxSessionCache` existe com `Lazy`, timeout idle de 15 min e timer; critério de LRU não aparece implementado; cache indexado só por modelId.

**Spec necessária antes de otimização:** chave deve identificar tenant + ID estável + versão/hash do modelo e opções de execução; política de expiração/limite de capacidade; concorrência no load/eviction; evitar devolver sessão já descartada durante inferência (lifetime/lease); update/delete do modelo invalida cache; métricas/limites de memória; fallback CPU identificável.

**Aceite:** mesma chave carrega uma vez sob concorrência; versões/tenants nunca reutilizam bytes/sessões; remoção não dispõe uma sessão em uso; idle TTL e max capacity demonstrados; output funcional; teste de cache não mede “<50ms” sem benchmark de baseline e hardware.

**Fonte:** `OnnxSessionCache.cs`, `DynamicOnnxProcessorTool.cs`, #74/#80. A latência <50ms só se torna SLO após ambiente de benchmark definido.

## issue-75

**Epic integração pilares no chat**

**Classificação:** roadmap antigo concluído parcialmente e sobreposto a #92/#93, além de funcionalidades novas em #120–123.

**Spec reconstituída:** manter apenas lista de capacidades aceitas/operacionais com link a issue focal; status por capacidade (runtime/UI/validation); exclusões de lab/preview; não exigir que todo pilar esteja “sob chat principal” sem caso de uso/autorizações.

**Fontes:** plano `unified-chat-integration.md`, ADR-022, stories específicas e #92/#93/#120–123. Recomendar tornar este epic índice histórico após reconciliação.

## issue-74

**ONNX in-process**

**Classificação:** issue tem ADR, US-45–47 e plano concluído; description mantém todas as caixas desmarcadas.

**Aceite existente a reconciliar:** upload/CRUD/inspect/test; tool de inferência; split weights e isolamento; path traversal e delete safe. A especificação atual deve usar US-45–47 como critério canônico e anotar limitações de métricas e validação física.

**Fontes:** ADR-010, user stories, `docs/plan/completed/onnx-in-process.md`, #80/#76. Recomendar fechar como entregue depois de revisar checklist/limites de segurança com o owner.

## Issues de multi-provider e CI

## issue-66

**Testes e auditoria multi-keys**

**Classificação:** checklist legado; falta distinguir cobertura funcional, scanners e gate geral.

**Aceite revisado:** CRUD tenant-scoped; segredo não retornado/logado; provider real/stub local validado; modelo e chave usados no chat; acesso por papel; casos de erro. Scanners são execução datada com versão/relatório. Percentual de cobertura aparece separado e não é aceite funcional.

**Fontes:** US-42, ADR-020, #61–#65/#123 e testes BYOK. Reconciliar testes antigos `LLMProviderApiKeyTests` com nomes atuais, não exigir classe/path literal.

## issue-65

**UI de API keys**

**Classificação:** tela `/ai` e fluxos BYOK existem em forma evoluída; issue fala em “UI premium” e seletor [modelo,nome da credencial], requisitos visuais não mensuráveis.

**Aceite:** lista com metadados mascarados; Owner/Admin muta; membro lê preferências; testar/discover/default/delete com estados de erro; segredo nunca exibido após salvar; chave default e modelo demonstrados no chat; foco keyboard/feedback e role gating.

**Fontes:** [US-42](../user-stories/us-multi-provider-api-keys.md), ADR-020, PR #124/issue #123, docs de chat settings. Não reproduzir todo o editor de administração global no escopo tenant.

## issue-64

**Endpoints API multi-keys**

**Classificação:** endpoints existem, mas nomes de controller e rotas do issue podem estar defasados.

**Aceite:** documentar/verificar rotas atuais do `LLMProviderApiKeyController`; tenant + provider scoping; roles; 404 para resource de outro tenant; POST/PUT/list sem valor secreto; `/test` consulta provider configurado; discover persiste modelos; status de erro não finge sucesso.

**Fontes:** endpoint inventory, `docs/backend/chat-sessions-settings.md`, controller atual, ADR-020, issue #123. Não exigir que a implementação permaneça em `LLMController.cs`.

## issue-63

**Core multi-keys e resolução**

**Classificação:** implementação existe; proposta de fallback de chave global não reflete o limite atual BYOK/Platform Admin.

**Aceite:** default habilitado é escolhido no provider/tenant; chave explícita request-only; secret ciphertext at-rest; DTO/log redact; modelo disponível; falha sem fallback silencioso; principal identifica API-key user opaco.

**Fonte:** ADR-020, current `LLMManager`, US-42, #120/#123. Rever separação entre BYOK de tenant e config global de plataforma antes de preservar requisito de fallback legado.

## issue-62

**Persistência de multi-keys**

**Classificação:** migration/model de credenciais já existem com nomes/migrações atuais.

**Aceite:** índice/uniqueness conforme regra atual; tenant/provider; ciphertext; backfill compatível; migration rollback/destructive impact explicitado; tests PostgreSQL em Compose. Remover path antigo `PersistenceEntities.cs`/nomes legados da prescrição.

**Fontes:** ADR-020, migrations atuais, `docs/backend/validation`, issue #123. Recomendação de fechamento após PR #73/123 revisar integração.

## issue-60

**Pipelines backend/frontend**

**Classificação:** corpo contém erros TypeScript antigos e pede Playwright; a configuração atual precisa ser comparada antes de usar como escopo.

**Aceite revisado:** PR workflow executa build/test backend e lint/build/frontend conforme arquivos reais; E2E tem runner explicitamente escolhido (Cypress existe hoje; Playwright só se decisão for aprovada); secrets ausentes não causam exposição; coverage gate 80% permanece; status checks configurados como required só quando workflows estiverem estáveis.

**Fontes:** `.github/workflows`, `package.json`, CI plan; requisitos E2E em #123. Corrigir links `file:///`, erros TS obsoletos e alinhar runner antes de retomar a issue.

## Issues de interface, memória e stories avançadas

## issue-45

**Gaps de telas**

**Classificação:** diagnóstico mostra as três subtarefas #46–#48 concluídas; não especifica gaps atuais.

**Aceite:** fechar checklist com evidência da UI atual ou substituir por issues individuais para qualquer gap observado; indicar browser/roles/fluxos; não manter epic aberto como backlog genérico.

**Fonte:** issue #45, planos completos de UI, docs de frontend. Recomendação: resolver status após revisão visual atual e relação das subissues.

## issue-34

**Histórico de sessão e memória de longo prazo**

**Classificação:** especificação antiga sobre sessão/listagem está parcialmente coberta por #123; consolidator/memória semântica é trabalho distinto.

**Spec revisada:** separar Session history (create/list/resume/end/owner/tenant/history) de Memory consolidation (summarization, retention, retrieval, deletion/consent, PII, limits). Especificar trigger do encerramento, idempotência, armazenagem de summaries, recuperação e exclusão. “memórias relevantes do usuário” precisa explicar tenant/user scope, ranking, ACL, opt-out e prova de não vazamento.

**Fontes:** #123, `PostgresSessionStore`, `SessionConsolidator`, story antiga, plano `refactoring-session-list.md`. Recomendação: dividir issue ou declarar sessão coberta e deixar somente memória com nova acceptance.

## issue-16

**ML39/FinOps e capacidades recentes**

**Classificação:** descrição contém três capacidades distintas e não acceptance; story ML39 já existe mas trata controle de quota/budget e auto-melhoria de forma combinada.

**Spec necessária:** quota proativa: ator, limite, forecast, prevenção/delivery; batch self-improvement: dataset/approval/privacy/version/rollback/eval; `DotNetExpertAgent`: usuário, tool scope, sources, quality eval, tenant access. Não implantar nenhum dos três sem casos e owners. Esclarecer se `DotNetExpertAgent` continua requisito ou exemplo antigo.

**Fonte:** ML39 em `USER-STORIES.md`; `docs/backend/resources-rules.md`; issue #16. Dividir em stories/novas issues se todos permanecerem escopo.

## issue-14

**ML37 Platform Capabilities**

**Classificação:** lista de dez temas, sem user stories com aceite individual.

**Spec necessária:** criar story/issue separada para Quality Gates, cleanup, vision, MCP gateway, storage, execution workflow, streaming, governance, artifacts e HITL; cada uma identifica ator, workflow, permissões, dados, erro, verificação, dependência e maturity/feature flag. Agrupar só temas que compartilham actor+runtime. Vision/MCP preview deve ficar condicionado a compatibilidade e security model.

**Fonte:** apêndice B da arquitetura citada no issue; cruzar com `USER-STORIES.md`, ADRs 036–038, docs de workflows/transportes. Não inferir que menções existentes equivalem a histórias completas.

## issue-13

**ML36 Smart Triage**

**Classificação:** descrição pede ML36, mas o catálogo atual nomeia Smart Triage como ML35; o issue parece duplicado/deslocado no ID.

**Spec necessária:** uma story por camada Fast Path/ML-local/LLM apenas se capacidade não estiver entregue; critérios para precisão/recall, confidence threshold, fallback, explicabilidade, modelo/version, custo/latência e tratamento de prompt/tenant; dizer qual layer está ativa no runtime e qual é proposta futura.

**Fontes:** story ML35, ADR-002, current triage code e issue #13. Resolver numeração ML35 vs ML36 antes de adicionar nova story; não duplicar Smart Triage.

## issue-12

**Atualizar user stories ML36–39**

**Classificação:** epic ainda lista subissues pendentes, mas os IDs/descrições divergiram do catálogo atual; #15 não aparece entre as issues abertas do snapshot.

**Aceite revisado:** matriz ML36–ML39 com IDs únicos, status por capability, referências para histórias completas; reconciliar #13/#14/#15/#16, remover links inexistentes e distinguir existente/implementado/proposto. Não alterar histórias históricas silenciosamente.

**Fonte:** `docs/USER-STORIES.md`, status de #13/#14/#15/#16 na API e documento de roadmap. #15 precisa ser verificada individualmente; manter a referência ou encerramento correto.

## Notas de fechamento

- Issues em PR draft #118, #119 e #124 permanecem abertas até revisão/merge e gates; issues com acceptance cumprida recebem recomendação, não fechamento automático.
- Requisitos que contradizem código são marcados como drift, não copiados como alvo novo sem confirmação.
- Propostas de arquitetura sem evidência permanecem `proposta` e precisam de decisão antes de virar implementação.
