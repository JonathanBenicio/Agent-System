# Backlog da auditoria do backend
Baseline f8de7a6; [epic #110](https://github.com/JonathanBenicio/Agent-System/issues/110). [Evidências executadas](validation/2026-09-28.md). Correções de produção estão fora desta entrega.

## Prioridade de correção
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
| Cobertura | 22,39% versus 80% exigidos | [#88](https://github.com/JonathanBenicio/Agent-System/issues/88), [#84](https://github.com/JonathanBenicio/Agent-System/issues/84) |
| Streaming e grupos | chat funcional falhou; outros hubs/protocolos não profundamente validados | [#86](https://github.com/JonathanBenicio/Agent-System/issues/86), #112/#114 |
| Upload/limpeza de documentos | overwrite físico por mesmo nome, exposição fileDiskPath e quota sem bytes reais inferidos por leitura; purga completa não testada | [#93](https://github.com/JonathanBenicio/Agent-System/issues/93), #117 |
| ONNX stats | loaded inferido de paths/config, avgLatencyMs=12.4 literal | [#74](https://github.com/JonathanBenicio/Agent-System/issues/74); [matriz](resources-rules.md) |
| Durable/workflows/evaluation | retomada multiinstância, RunId/polling e cache não comprovados | [#108](https://github.com/JonathanBenicio/Agent-System/issues/108), [revisão anterior](../plan/pending-changes-review-2026-09-28.md) |
| Dependências | restore reportou NU1903 em Microsoft.OpenApi2.4.1 e SQLitePCLRaw2.1.11, transitive da baseline | registrar atualização/testes em iniciativa de dependências; sem mudança automática nesta auditoria |
| Voice e contratos fora do núcleo | inventariados por fonte; identidade enviada pelo cliente e timeout exigem auditoria específica | inventário e futuras stories; não validados em execução |
| Licença | README declara MIT mas LICENSE não existe | formalização pendente; não criar licença sem decisão do mantenedor |

## Ordem sugerida
1. Corrigir #111/#112/#113 com cenários negativos reais e provas de não vazamento.
2. Corrigir #114/#116 e provar conversa, retomada e streaming bem-sucedidos nos três transportes.
3. Corrigir #115, definir política de falhas/retries e testar concorrência/rollover/cache/multiinstância.
4. Implementar #117 com ADR/story/plano próprios, migração e matriz ator/recurso/ação.
5. Ampliar cobertura e validar módulos restantes antes de declarar prontidão.

Cada correção deve atualizar issue → ADR → story → plano → commits/PR e evidências. O relatório da auditoria não substitui o plano de implementação da correção.
