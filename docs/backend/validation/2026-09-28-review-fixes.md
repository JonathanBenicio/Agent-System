# Correções da revisão do PR #118 — 2026-09-28

[PR](https://github.com/JonathanBenicio/Agent-System/pull/118) · [Plano](../../plan/backend-documentation-validation.md) · [Resultados e hashes](2026-09-28-review-fixes-results.json) · [Diagnóstico histórico](2026-09-28.md).

Os quatro achados da revisão foram tratados. Código/schema de produção permanece na baseline f8de7a6; mudanças desta rodada são no harness, consolidado e documentação. Os bugs de produto #111–#117 continuam abertos.

| Achado | Correção | Verificação |
|---|---|---|
| P1 — base ausente | Base remota incorporada no merge 68ea51e | Harness compilou e fontes/links existem |
| P1 — falsos positivos de isolamento | Chat exige HTTP 403/404; hub exige controle positivo aprovado e negação explícita; erros genéricos não passam | Regressões com HTTP 200/success=false, 500, timeout, desconexão, JSON inválido e erro de provider |
| P2 — totais fixos do relatório | Counters do TRX e métricas do Cobertura são lidos; manifesto vincula revisão/ambiente/hashes e cobertura exata | TRX original: 687 passaram, 1 não executado/ignorado, 0 falhas; regressões com falha/skip/outcome incompleto, XML inválido e artefato substituído |
| P2 — conteúdo das mensagens | Fixture conhecida pelo PostgresSessionStore real; snapshot de IDs/papéis/conteúdo/ordem/timestamps antes/depois | Duas mensagens, título e ownership preservados após restart da API; mensagens vazias/modificadas/reordenadas recusadas |

## Execução

Seis testes de regressão passaram, zero falhas. Sintaxe dos scripts Node válida. O consolidado histórico foi regenerado somente dos artefatos originais e mantém 24/9/2 cenários e cobertura 22,39%; não representa nova execução desses 35 cenários.

Reexecução real parcial: **20 passaram, 6 falharam, 2 não executados**, 28 cenários de core/session. PG16.15/pgvector0.8.6 e Ollama0.34.4 reais, modelos/configuração/portas do ambiente isolado original. Ollama direto, ingestão, ACL e controle positivo de hub passaram. API key Viewer, query de tenant nos hubs e chat/SSE/SignalR continuam reproduzindo os bugs de produto. CHAT-02 continua não executado por ausência de conversa bem-sucedida; a regressão controlada comprova a correção da asserção, não o isolamento do produto.

O primeiro teste corrigido de mensagens rejeitou uma sessão vazia de chat falho. O harness passou a criar fixture sintética conhecida para testar CRUD/persistência independentemente do LLM. Não apresentar isso como prova de geração ou retomada de conversa bem-sucedida. As cinco verificações de sessão/ownership/título/mensagens passaram com a fixture, incluindo a comparação após parada/início real da API.

Comandos: `node --test tests/backend-validation/evidence.test.mjs`, `node scripts/build-backend-validation-report.mjs`, `node tests/backend-validation/core-diagnostics.mjs`, `dotnet run --project tests/backend-validation/BackendDiagnostics.csproj --configuration Release -- --session-fixture`, `node tests/backend-validation/session-diagnostics.mjs`, parada/início isolados e `node tests/backend-validation/session-diagnostics.mjs --after-restart`. [Setup e ordem de reprodução](../operations.md).

O C# compilou ao criar a fixture. A primeira tentativa enquanto a API estava ativa encontrou DLLs bloqueadas no Windows; a API foi parada e a compilação terminou com sucesso. O guia agora manda compilar antes de iniciar a API e executar com --no-build. NU1903/CS8600 preexistentes permanecem. Suíte completa xUnit, store/quotas, cobertura e frontend não reexecutados nesta rodada.

Artefatos novos em tests/TestResults/backend-documentation/current; históricos preservados e vinculados por manifesto. Nenhum segredo real publicado. API/compose isolados parados ao terminar; volumes conservados. PR permanece draft por qualidade e bugs de produto; merge/deploy não executados.
