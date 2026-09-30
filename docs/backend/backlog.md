# Backlog da auditoria do backend
Baseline f8de7a6; [epic #110](https://github.com/JonathanBenicio/Agent-System/issues/110). A auditoria original permanece em [evidências históricas](validation/2026-09-28.md); os resultados da remediação na branch estão em [backend-core-remediation](validation/backend-core-remediation.md). A branch permanece em PR draft.

## Estado da remediação #111–#117 — 2026-09-29

| Issue | Estado nesta branch | Limite restante |
|---|---|---|
| #111 | Corrigida nos cenários testados; API key e bearer opaco conservam o papel da membership. | Revisão da issue permanece aberta; sem promoção automática a Platform Admin. |
| #112 | Seleção/handshake e eventos dos cinco hubs testados; Gateway REST/hub exige Platform Admin e publica no tenant autorizado. | Gateway de produção ainda não registra providers no `IServiceGateway`; o evento foi acionado por fixture Validation. |
| #113 | SQL fail-closed e resposta RAG real com sala ACL passaram. | Fixture usa documentos/frases sintéticos. |
| #114 | Sessão REST/SSE/SignalR, ownership, MAF serialized state e retomada após restart passaram. | — |
| #115 | 32 increments concorrentes persistiram; uso real tokens/custo e bloqueio REST/SSE/OpenAI-compatível após restart passaram. | RPM é por processo; dois hosts de API não foram testados. |
| #116 | Catálogo completo/tenant-scoped, customizações e IDs estáveis após restart. | — |
| #117 | Membership endpoints, backfill, grants ACL/audit, planos e limites foram exercitados. | Backfill é fixture sintética; A2A/AG-UI e cópia representativa de produção ficaram fora. |

As issues permanecem abertas para revisão conjunta; status de implementação neste backlog não fecha issues automaticamente.

## Baseline da auditoria — prioridades originais
| Prioridade | Lacuna | Evidência | Issue |
|---|---|---|---|
| P0 | API key Viewer promovida a Admin e troca de tenant aceita | AUTH-05, HTTP 200 | [#111](https://github.com/JonathanBenicio/Agent-System/issues/111) |
| P0 | Query SignalR seleciona tenant divergente/desconhecido | HUB-02/03, método completou | [#112](https://github.com/JonathanBenicio/Agent-System/issues/112) |
| P0 | room_ids vazio retorna documentos; filtro não vazio quebra SQL | STORE-02/03, SQLSTATE 42703 | [#113](https://github.com/JonathanBenicio/Agent-System/issues/113) |
| P1 | Chat REST/SSE/SignalR falha por chave de isolamento do MAF | CHAT-01/SSE-01/HUB-04, success=false | [#114](https://github.com/JonathanBenicio/Agent-System/issues/114) |
| P1 | Incrementos concorrentes esgotam retries de quota | QUOTA-02: 2/8 falhas explícitas | [#115](https://github.com/JonathanBenicio/Agent-System/issues/115) |
| P1 | Seeding de skills por tenant colide em IDs globais | log SQLSTATE 23505 PK_agent_skills | [#116](https://github.com/JonathanBenicio/Agent-System/issues/116) |
| P1 | Memberships, suporte temporário, papéis e limites sem modelo/enforcement unificado | leitura do código; alvo ainda ausente | [#117](https://github.com/JonathanBenicio/Agent-System/issues/117) |

P0 indica bloqueio de boundary/segurança; não implica que todos os caminhos de exploração ou vazamentos de conteúdo tenham sido demonstrados. P1 indica falha do núcleo/confiabilidade ou desenho necessário.

## Pendências adicionais e trabalho existente
| Frente | Estado/evidência | Acompanhamento |
|---|---|---|
| Cobertura | Última medição disponível antes do follow-up: 21,08% versus 80% exigidos; não foi prioridade desta rodada | [#88](https://github.com/JonathanBenicio/Agent-System/issues/88), [#84](https://github.com/JonathanBenicio/Agent-System/issues/84) |
| Streaming e grupos | chat REST/SSE/SignalR e isolamento dos cinco hubs foram exercitados; A2A/AG-UI não foram habilitados no host de validação | [#86](https://github.com/JonathanBenicio/Agent-System/issues/86), #112/#114 |
| Upload/limpeza de documentos | quota conta bytes lógicos de origem; fileDiskPath continua exposto, cópia física pode ser sobrescrita por mesmo nome e purga completa não foi comprovada | [#93](https://github.com/JonathanBenicio/Agent-System/issues/93), #117 |
| ONNX stats | loaded inferido de paths/config, avgLatencyMs=12.4 literal | [#74](https://github.com/JonathanBenicio/Agent-System/issues/74); [matriz](resources-rules.md) |
| Durable/workflows/evaluation | retomada multiinstância, RunId/polling e cache não comprovados | [#108](https://github.com/JonathanBenicio/Agent-System/issues/108), [revisão anterior](../plan/pending-changes-review-2026-09-28.md) |
| Dependências | Microsoft.OpenApi, SQLitePCLRaw e ImageSharp foram atualizados; última auditoria NuGet não encontrou vulnerabilidades conhecidas | validar novamente quando o lockfile/pacotes mudarem |
| Voice e contratos fora do núcleo | inventariados por fonte; identidade enviada pelo cliente e timeout exigem auditoria específica | inventário e futuras stories; não validados em execução |
| Licença | README declara MIT mas LICENSE não existe | formalização pendente; não criar licença sem decisão do mantenedor |

## Ordem sugerida
1. Corrigir #111/#112/#113 com cenários negativos reais e provas de não vazamento.
2. Corrigir #114/#116 e provar conversa, retomada e streaming bem-sucedidos nos três transportes.
3. Corrigir #115, definir política de falhas/retries e testar concorrência/rollover/cache/multiinstância.
4. Implementar #117 com ADR/story/plano próprios, migração e matriz ator/recurso/ação.
5. Ampliar cobertura e validar módulos restantes antes de declarar prontidão.

Cada correção deve atualizar issue → ADR → story → plano → commits/PR e evidências. O relatório da auditoria não substitui o plano de implementação da correção.
