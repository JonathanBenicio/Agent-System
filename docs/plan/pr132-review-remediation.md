# Plano — corrigir os 32 achados do PR #132

Status: em execução. Prioridade: P1 — os achados incluem falhas de autorização, isolamento e contratos do núcleo.

Issue principal: [épico #139](https://github.com/JonathanBenicio/Agent-System/issues/139). ADR: [041](../architecture/adr/041-pr132-review-remediation.md). Stories: [BACK-REVIEW-139](../USER-STORIES.md#back-review-139--corrigir-os-32-achados-do-pr-132).

Branch de trabalho: `codex/pr132-review-remediation`, criada sobre `integration/develop-pr-stack-2026-09-30` (base do PR #132). O PR #152 aponta para essa mesma branch.

## Objetivo e decisões

Corrigir e validar R01–R32 em 12 subtarefas, agrupadas por contexto. Cada commit tem título e corpo em PT-BR, descreve as alterações e referencia as issues relacionadas. Usar `Refs #ID` enquanto o escopo estiver parcial; usar `Closes #ID` apenas quando todos os critérios da issue estiverem comprovados e o fechamento for intencional.

A atualização de governança solicitada no chat “Revisar documentação do projeto” está no commit `169c3f8` e contribui para R30. Isso, por si só, não conclui a subtarefa #150.

O usuário autorizou validar PostgreSQL com o Compose do repositório ou porta isolada. Os testes desta entrega usam o Compose `tests/backend-validation/compose.yml`, publicado em `127.0.0.1:55432`, com bases exclusivas `review_pr152_*`. Não tocar a porta 5432, outros projetos, bancos ou volumes. O produto poderá iniciar com banco vazio; a cadeia de migrations precisa criar o schema atual.

DurableTask permanece opcional. MAF 1.23, A2A/AG-UI em preview, Gateway e provider de produção, retomada de sessões do supervisor entre hosts, forecast e PRs #31/#73 permanecem fora desta entrega e têm planejamento próprio.

## Disposição das issues preexistentes

Estado conferido no GitHub em 2026-10-02: as issues abaixo permanecem abertas. Nenhuma issue antiga será fechada apenas porque um trecho relacionado entrou neste PR.

| Issue(s) | Disposição registrada |
|---|---|
| [#31](https://github.com/JonathanBenicio/Agent-System/issues/31) e [#73](https://github.com/JonathanBenicio/Agent-System/issues/73) | Avaliar o cleanup/documentação e a pilha legada depois do merge do #132, conforme decisão do usuário. |
| [#97](https://github.com/JonathanBenicio/Agent-System/issues/97) | Adiado para depois do #132; manter aberto. O novo tipo de contexto de sistema não fecha os demais critérios da issue. |
| [#120](https://github.com/JonathanBenicio/Agent-System/issues/120), [#122](https://github.com/JonathanBenicio/Agent-System/issues/122), [#133](https://github.com/JonathanBenicio/Agent-System/issues/133), [#134](https://github.com/JonathanBenicio/Agent-System/issues/134) | Provider/Gateway de produção e retomada de supervisor entre hosts ficam para planejamento/trabalho futuro. Correção SQL opcional não prova esses fluxos. |
| [#121](https://github.com/JonathanBenicio/Agent-System/issues/121) e [#135](https://github.com/JonathanBenicio/Agent-System/issues/135) | Validação de protocolos preview e previsão FinOps continuam futuros. |
| [#16](https://github.com/JonathanBenicio/Agent-System/issues/16) | Quotas proativas, processamento em lote e `DotNetExpertAgent` devem permanecer em stories/issues separadas; esta epic não fecha o restante da visão. |
| [#99](https://github.com/JonathanBenicio/Agent-System/issues/99), [#104](https://github.com/JonathanBenicio/Agent-System/issues/104), [#105](https://github.com/JonathanBenicio/Agent-System/issues/105), [#106](https://github.com/JonathanBenicio/Agent-System/issues/106), [#109](https://github.com/JonathanBenicio/Agent-System/issues/109) | Decisões de produto e código relacionados foram tratados em contexto; as issues completas permanecem abertas até seus critérios próprios serem confrontados com evidência. |
| [#110](https://github.com/JonathanBenicio/Agent-System/issues/110), [#111](https://github.com/JonathanBenicio/Agent-System/issues/111), [#113](https://github.com/JonathanBenicio/Agent-System/issues/113), [#115](https://github.com/JonathanBenicio/Agent-System/issues/115), [#117](https://github.com/JonathanBenicio/Agent-System/issues/117), [#123](https://github.com/JonathanBenicio/Agent-System/issues/123) | Mantidas abertas. A contribuição dos achados #140–#150 não substitui a avaliação integral das issues originais. |

## Subtarefas e estado

| Issue | Achados | Story | Estado atual |
|---|---|---|---|
| [#140 — Autorização, cookie e preview](https://github.com/JonathanBenicio/Agent-System/issues/140) | R02, R03, R20, R21 | BACK-REVIEW-139-01 | Implementada; regressões backend e Chromium passaram; integração real com provider não é necessária para estes critérios. |
| [#141 — Proteção FIDES nos despachos MAF](https://github.com/JonathanBenicio/Agent-System/issues/141) | R01 | BACK-REVIEW-139-02 | Implementada; chamadas diretas, streaming e tools passaram; OCR de imagem/PDF e persistência tenant-scoped passaram em testes dedicados com dados/DB isolados. |
| [#142 — Limites seguros para ONNX](https://github.com/JonathanBenicio/Agent-System/issues/142) | R04 | BACK-REVIEW-139-03 | Implementada; regressões de dimensões, canais, overflow e orçamento passaram. |
| [#143 — ACL, RAG e isolamento](https://github.com/JonathanBenicio/Agent-System/issues/143) | R06, R07, R19, R23 | BACK-REVIEW-139-04 | Implementada; regressões API, stores, jornada RAG e filtro `room_ids` em PostgreSQL real passaram. Pinecone permanece validado por HTTP fake. |
| [#144 — Quotas e sessões](https://github.com/JonathanBenicio/Agent-System/issues/144) | R08–R11 | BACK-REVIEW-139-05 | Implementada; reset/cancelamento, REST/SSE/SignalR, retomada no teto, concorrência de sessões e persistência de uso PostgreSQL passaram. |
| [#145 — Workflows e aprovação](https://github.com/JonathanBenicio/Agent-System/issues/145) | R12–R16, R25 | BACK-REVIEW-139-06 | Implementada; backend, PostgreSQL, round-trip e decisões UI com erros passaram. |
| [#146 — Escopo de analytics](https://github.com/JonathanBenicio/Agent-System/issues/146) | R17 | BACK-REVIEW-139-07 | Implementada; consultas aguardam a vida do escopo e mantêm isolamento de tenant. |
| [#147 — Cache de memória](https://github.com/JonathanBenicio/Agent-System/issues/147) | R18 | BACK-REVIEW-139-08 | Implementada; consulta, filtros, usuário, tenant e invalidação são testados. |
| [#148 — Rotas de tools e YAML](https://github.com/JonathanBenicio/Agent-System/issues/148) | R22, R24 | BACK-REVIEW-139-09 | Implementada; API, manifesto, persistência PostgreSQL e round-trip visual/YAML passaram. |
| [#149 — CI, E2E e diagnósticos isolados](https://github.com/JonathanBenicio/Agent-System/issues/149) | R05, R26–R28, R31 | BACK-REVIEW-139-10 | Implementada localmente; lint/build, Playwright Chromium+Firefox e Cypress passaram. Falta o resultado do CI remoto após publicar os commits finais. |
| [#150 — IDs e documentação](https://github.com/JonathanBenicio/Agent-System/issues/150) | R29, R30 | BACK-REVIEW-139-11 | IDs, regras e auditoria das issues originais concluídos; checker encontrou 169 documentos e 936 links sem destinos quebrados. Atualizar descrições públicas e relacionar apenas subtarefas completas no PR após CI. |
| [#151 — SQL opcional DurableTask](https://github.com/JonathanBenicio/Agent-System/issues/151) | R32 | BACK-REVIEW-139-12 | Probes de lote, rollback e concorrência passaram numa base nova após aplicar as 20 migrations; store EF comparado ao schema (70 tabelas). |

## Matriz dos achados

| ID | Defeito | Contexto principal | Estado/evidência |
|---|---|---|---|
| R01 | FIDES não protegia a execução direta MAF | #141 | Factory e supervisor usam cliente protegido; chamadas diretas, streaming e tools exercitados com provider fake. |
| R02 | Authorization cru podia ignorar membership | #140 | TestServer cobre chave sem `Bearer`, tenant ativo, membership, papel e rejeição antes da execução. |
| R03 | Preview de skill permitia conteúdo Markdown ativo | #140 | Chromium confirma renderização e bloqueio de HTML, handlers e links `javascript:`. |
| R04 | ONNX alocava antes de limitar dimensão e bytes | #142 | Valida canais, overflow, dimensão máxima e orçamento antes do preprocessing/alocação. |
| R05 | CI E2E não iniciava nem aguardava o frontend | #149 | Workflow executa lint/build, Playwright e Cypress com servidores gerenciados; CI remoto pendente. |
| R06 | Upload em sala não exigia papel de escrita | #143 | Editor/Admin permitido; Reader e papel insuficiente negados antes de persistir. |
| R07 | Repositório dinâmico InMemory não particionava agentes por tenant | #143 | Mesmo nome em tenants A/B permanece isolado, inclusive leitura e desativação. |
| R08 | Quota diária InMemory não fazia reset correto | #144 | Clock determinístico cobre virada UTC, isolamento e concorrência. |
| R09 | Cancelamento/falha de stream perdia consumo recebido | #144 | Finalizadores contabilizam uso informado ou estimativa sem dupla contagem. |
| R10 | Contagem de sessões era truncada pelo limite histórico | #144 | Consulta conta todas as sessões ativas; sessões encerradas não escondem sessões antigas ativas. |
| R11 | Rotas de chat aplicavam limites de sessão diferentes | #144 | REST, SSE, SignalR e chamada direta bloqueiam nova sessão no teto e permitem retomar a própria sessão. |
| R12 | Enum de workflow divergente entre API e cliente | #145 | JSON publica strings estáveis; snapshots numéricos antigos e todos os tipos fazem round-trip. |
| R13 | Múltiplas aprovações eram ambíguas | #145 | Aprovar/rejeitar aceita `stepId`; cada aprovação paralela é individual. |
| R14 | Saídas paralelas compartilhavam estado sem sincronização | #145 | Execução paralela aguarda branches e mescla resultados deterministamente; regression cobre concorrência real. |
| R15 | Grafo inválido podia ser persistido | #145 | Dependências ausentes, ciclos e edges divergentes são rejeitados antes de salvar/executar. |
| R16 | Worker fazia claim global sem capability tipada | #145 | Claim exige `SystemOperationContext`; execução de dados de negócio abre escopo de tenant real. |
| R17 | Consulta de analytics sobrevivia ao escopo de DbContext | #146 | Provider DI com escopo real valida os comandos e isolamento A/B. |
| R18 | Cache de memória não distinguia consulta/filtros | #147 | Chaves variam por consulta e filtros; contexto vazio e invalidação são cobertos. |
| R19 | Filtro `room_ids` divergente ou permissivo em fallback | #143 | Lista vazia nega acesso; PostgreSQL real exclui 55 documentos recentes de outra sala antes do limite e retorna os cinco autorizados. |
| R20 | Logout não invalidava o cookie no servidor | #140 | Chromium confirma chamada de logout, expiração de cookie e estado de falha correto. |
| R21 | Chave de API era persistida no localStorage | #140 | Login/restauração por cookie não grava nem envia a chave em armazenamento JS. |
| R22 | Rotas de tools divergiam do contrato publicado | #148 | `list/get/execute/delete` seguem `/api/agent/tools`; autorização precede o manager. |
| R23 | Upload/RAG confundia `roomId` com origem/chunks | #143 | Browser/API e regressões verificam associação à sala/tenant esperado. |
| R24 | Template e serializer YAML divergiam do DTO | #148 | Manifesto oficial valida; UI alterna, salva e reabre descrição, instruções, ferramentas e configuração. |
| R25 | UI confirmava approval após resposta HTTP de erro | #145 | 403/409/500 mantêm pendência, mostram erro e não exibem decisão confirmada. |
| R26 | Teste XSS não verificava payload renderizado | #149 | Teste verifica conteúdo visível e que HTML/handler/link malicioso não executa. |
| R27 | Teste de timeout dependia de duração não confiável | #149 | Timer controlável valida timeout, limpeza do indicador e recuperação. |
| R28 | Diagnósticos Compose podiam atingir projeto/banco alheio | #149 | API, C# e consultas Node exigem projeto e DB `review_pr152_*` iguais; manifesto de runtime é comparado antes dos diagnósticos. Teste negativo rejeita divergência e DB genérico. |
| R29 | Identificador de story duplicado | #150 | Story multi-key usa `BACK-KEYS-020`; US-42 de FinOps e referências existentes são preservadas. |
| R30 | Instrução GEMINI sugeria `Closes` indevido | #150 | Regras PT-BR distinguem `Refs` de `Closes` e descrevem alterações no corpo do commit. |
| R31 | Cypress apontava para servidor/API incorretos | #149 | Script inicia Vite isolado em 5193 e encerra-o; smoke test usa contrato REST atual. |
| R32 | `dt.complete_tasks` usava `DISTINCT ... FOR UPDATE` e contagem agregada | #151 | Migration de reparo usa lock por instância; probes cobrem lotes, rollback, terminal e concorrência. |

## Verificação atual

| Verificação | Resultado | Limites |
|---|---|---|
| Suíte completa .NET 10 (`dotnet test --no-restore`) | 923 aprovados, 25 ignorados, 0 falhas; 948 testes | Default não define PostgreSQL/OCR/Hyperlight; os gates condicionais foram executados separadamente. Hyperlight: 8/8 com `AGENTIC_TEST_HYPERLIGHT=true` no host Windows com hypervisor. |
| Regressões por contexto | #140: 23 backend e 4 Chromium; #141: 28 FIDES/MAF, 2 OCR, 1 PostgreSQL; #142: 49; #143: 65 regressões + 1 PostgreSQL real; #144: 28 + HTTP + 2 PostgreSQL; #145: 34 backend + 8 browser + 5 PostgreSQL; #146/#147: 14; #148: rotas/manifesto/persistência; #149: verificações abaixo | Doubles, EF InMemory e HTTP fake estão identificados por contexto no relatório; não equivalem a provider externo real. |
| Playwright configurado para CI | 48 aprovados: 24 Chromium e 24 Firefox; 0 falhas | API mockada nos fluxos UI. Os testes de login que exigem API real ficam excluídos sem `REAL_E2E=true`; a regressão específica de approval também passou 8/8 no harness. |
| Cypress | 1 aprovado, 0 falhas; servidor Vite gerenciado pelo runner | Teste de UI com API mockada. |
| Frontend | `npm run lint` e `npm run build` passaram | Executado no WSL sobre este checkout. |
| PostgreSQL Compose isolado, banco novo | 20 migrations aplicadas; 70 tabelas public; teste de persistência dinâmica e 5 testes de workflow passaram | DB `review_pr152_dynamicagents_20261002`, Compose com porta 55432 e projeto exclusivos; DB removido e serviço parado, volume preservado. `has-pending-model-changes` passou. |
| SQL `dt.complete_tasks` e schema | PostgreSQL 16 isolado: 20 migrations, 70 tabelas públicas, modelo EF comparado, lotes 0/1/2/4, rollback, término e concorrência passaram | Base exclusiva removida e serviço parado; volume preservado. |
| Links/índices locais | 169 documentos, 936 links, 0 destinos quebrados | Passou após atualizar plano, ADR e relatório. |
| CI GitHub / estado final do PR | Pendente | Execução anterior: 923 aprovados, 25 ignorados, 0 falhas; build ficou vermelho apenas pela cobertura medida de 15,3% contra o threshold de 80%. Por decisão do usuário, cobertura virou aviso; aguardar nova execução remota. |

## Limites e condições de conclusão

Fixtures TestServer, EF InMemory, provider fake, Pinecone HTTP fake, OCR ignorado e PostgreSQL real são evidências diferentes e continuam identificadas. Esta entrega não demonstra provider/Gateway de produção, OCR/Tesseract no ambiente real, A2A/AG-UI em preview ou retomada multi-host do supervisor.

Não fechar issues antigas por atender apenas parte do escopo. Só relacionar no PR as issues cujos critérios completos estejam demonstrados. Antes de concluir a epic: revisar todos os arquivos staged/unstaged/untracked, concluir os commits por contexto, publicar a branch, aguardar CI remoto e verificar base/estado do PR #152.
