# Evidências — correções da revisão do PR #132

Data de início: 2026-10-02. Épico: [#139](https://github.com/JonathanBenicio/Agent-System/issues/139). Plano/matriz: [pr132-review-remediation.md](../../plan/pr132-review-remediation.md). ADR: [041](../../architecture/adr/041-pr132-review-remediation.md).

Branch: `codex/pr132-review-remediation`. Base do PR #152: `integration/develop-pr-stack-2026-09-30`, a branch do PR #132.

## Resultado por contexto

| Contexto | Issue | Evidência atual | Resultado e limite |
|---|---:|---|---|
| Autorização/cookie/preview | #140 | TestServer com handlers/controllers/middleware reais; 23 regressões backend. Playwright Chromium executou login, restore, logout, falha no logout e preview XSS. | 23 backend e 4 Chromium passaram. DB/permission store usa EF InMemory; chamadas de orquestração são doubles. |
| FIDES | #141 | 28 testes FIDES/MAF cobrem chamada direta, streaming, instruções, tools, política A/B e bloqueio antes do provider. Dois testes OCR usam PNG e PDF reais; teste PostgreSQL valida persistência/versionamento e isolamento da política. | 28 + 2 OCR + 1 PostgreSQL passaram. OCR usou o modelo inglês oficial baixado para uma pasta temporária; provider das regressões continuou fake. PostgreSQL usou DB exclusivo em 55432. |
| ONNX | #142 | 49 regressões cobrem dimensão/canais, overflow, orçamento, configuração, upload/update, worker e preprocessing com imagem pequena real. | 49 passaram. Não força OOM nem valida pesos/modelos de produção. |
| ACL/RAG/tenancy | #143 | 65 regressões de permissões, grants, isolamento A/B, filtros de salas e upload; Playwright inclui o fluxo RAG. Teste PostgreSQL real sem embeddings compara 55 documentos recentes de outra sala com cinco autorizados mais antigos. | 65 regressões e o filtro PostgreSQL passaram. A suite sem DB pula o teste PostgreSQL condicional. Pinecone continua HTTP fake; outras comparações de store usam InMemory/SQLite. |
| Quota/sessões | #144 | 28 testes de quota/stream/reserva e regressões HTTP para REST, SSE, SignalR e chamadas diretas. Teste PostgreSQL concorrente usa 12 instâncias de store; teste de quota grava e lê uso após recriar o repositório. | Regressões direcionadas passaram. PostgreSQL confirmou contador de tokens/custo/requisições após recriação e isolamento de tenant. A suite completa passou após substituir as fixtures NSubstitute por store InMemory real. |
| Workflows | #145 | 34 regressões de backend; 8 testes Chromium de enum/round-trip, múltiplas aprovações e respostas 403/409/500. Cinco testes PostgreSQL cobrem store, lease, espera e snapshot. | Backend/browser/PostgreSQL passaram. Dados de negócio usam store InMemory/fakes quando indicado; os cinco testes SQL foram executados na base isolada. |
| Analytics/memória | #146/#147 | 14 regressões com escopo DI real, EF InMemory, isolamento A/B, cache por consulta/filtros e invalidação. | 14 passaram. Não demonstra consultas analíticas em servidor PostgreSQL externo. |
| Tools/YAML | #148 | TestServer cobre rotas, membership revogada, papel Viewer, salvamento/edição da especificação e manifesto versionado. Playwright cobre edição visual/YAML com strings, escapes e configuração. PostgreSQL cobre persistência e isolamento após migration. | Testes direcionados e E2E passaram; migration e persistência foram validadas numa base nova descrita abaixo. |
| CI/E2E/diagnósticos | #149 | Workflow GitHub inicia frontend; Playwright tem `webServer`; Cypress inicia Vite em porta dedicada; fixtures/mocks usam contratos REST atuais. Helper SQL e launcher exigem o mesmo projeto/base e comparam o manifesto da API. | Lint/build passaram; Playwright configurado para CI passou 48/48 (24 Chromium e 24 Firefox); Cypress passou 1/1. Smoke local da API retornou 200 e a base `review_pr152_api_20261002` recebeu 20 migrations; CI remoto ainda precisa executar após o push final. |
| IDs/documentação | #150 | Story multi-key usa `BACK-KEYS-020`; US-42 de FinOps e referências históricas permanecem. Regras em AGENTS/GEMINI/workflow/templates dizem PT-BR, issue relacionada, descrição e `Refs`/`Closes`. Issues originais continuam abertas; decisões adiadas e escopos futuros estão na matriz. | R29/R30 implementados. Checker local encontrou 169 documentos, 936 links e 0 destinos quebrados. Falta sincronizar descrições públicas e Development do PR depois do CI. |
| SQL DurableTask | #151 | Migration `RepairDurableTaskCompletion`; teste de schema compara todas as colunas EF e executa probes de lote 0/1/2/4, rollback e instância terminal; segundo teste disputa duas conexões concorrentes. | 2 testes DurableTask e 1 teste de concorrência de sessão passaram na base nova, depois de 20 migrations. 70 tabelas públicas; modelo EF sem divergência. |

## Suíte completa e navegador

- `dotnet test --no-restore`: **923 aprovados, 25 ignorados, 0 falhas; 948 testes**. PostgreSQL, OCR, Ollama e Hyperlight condicionais foram ignorados quando as variáveis/serviços não foram configurados. A execução completa não recebeu `AGENTIC_TEST_POSTGRES`/`AGENTIC_REVIEW_POSTGRES`/`AGENTIC_TEST_HYPERLIGHT`; os gates condicionais foram executados separadamente.
- Playwright pelo comando padrão: **48 aprovadas, 0 falhas** — 24 em Chromium e 24 em Firefox. Os testes de login com API real são excluídos quando `REAL_E2E` está desligado; os fluxos UI incluídos usam API mockada e interface React real.
- Regressões de aprovação/workflow: **8 aprovadas** com a UI, store e hook reais; a API foi mockada para controlar HTTP 403/409/500.
- Cypress: **1 aprovado** em Vite iniciado pelo runner na porta 5193; a resposta REST usa API mockada.
- Frontend: `npm run lint` e `npm run build` passaram no WSL sobre este checkout.
- Hyperlight: execução explícita com `AGENTIC_TEST_HYPERLIGHT=true` no host Windows com hypervisor passou **8/8**. Runner Linux sem hypervisor pula apenas o teste oficial do sandbox; gates do feature flag continuam ativos.

## Banco novo e migrations

Compose: projetos exclusivos usaram PostgreSQL/pgvector 16, publicado apenas em `127.0.0.1:55432`. As bases temporárias `review_pr152_dynamicagents_20261002`, `review_pr152_gate_20261002`, `review_pr152_fides_20261002` e `review_pr152_quota_persistence_20261002` foram criadas e migradas do zero. A execução de schema aplicou **20 migrations**, comparou todas as colunas do modelo EF e encontrou **70 tabelas públicas**; os probes de `dt.complete_tasks` passaram para lotes, rollback, término e concorrência. Persistência dinâmica, política FIDES, contador de uso de quota e regressões de workflow/sessão também passaram. `dotnet ef migrations has-pending-model-changes` informou que o snapshot está sincronizado.

Depois dos testes, apenas os bancos temporários criados por esta validação foram removidos e o serviço do Compose foi parado; o volume da execução foi preservado. A porta 5432 e outros projetos/bancos não foram usados.

O helper `compose-target.mjs` foi exercitado: a consulta `SELECT 1` passou na base `review_pr152_*`; apontar para `backend_validation` foi recusado antes de executar SQL.

O smoke do launcher verificou manifesto `api-target.json`, `/health` com HTTP 200 e 20 migrations na mesma base consultada pelo helper Compose. O banco temporário foi removido e o serviço parado; não houve API ou banco preexistente na porta 5188/55432.

## Migration e SQL opcional DurableTask

`DurableTaskCompletionPostgresRegressionTests` foi executado numa base recém-criada depois das 20 migrations atuais. `FreshSchemaMatchesEfAndCompletesAtomicTaskBatches` comparou todas as colunas EF, validou o schema e executou os probes SQL: lotes 0/1/2/4, rollback sem efeitos parciais e instância terminal sem evento obsoleto. `CompetingCompletionPublishesExactlyOneResultAndRollsBackTheLoser` executou duas conexões concorrentes e observou um sucesso e um retry `40001`, com um evento e um payload. Ambos passaram. A sessão concorrente do PostgreSQL também passou nessa base.

## Ambientes e evidências anteriores

As execuções direcionadas registraram: #140 23 backend/4 Chromium; #141 28 testes FIDES/MAF, 2 OCR e 1 PostgreSQL; #142 49; #143 65 regressões e 1 filtro PostgreSQL real; #144 28, HTTP, concorrência e persistência PostgreSQL; #145 34 backend/8 browser/5 PostgreSQL; #146/#147 14. A suite completa default passou com 923 aprovados, 25 ignorados e zero falhas; Hyperlight passou 8/8 quando habilitado no host compatível.

Testes com provider fake provam o conteúdo enviado ao contrato fake e a ordem de bloqueio. Eles não provam disponibilidade, registro em produção, streaming de provider real ou integração Gateway. OCR de imagem e PDF passou em execução separada com o modelo inglês oficial; a suite completa sem configurar OCR e Hyperlight mantém os respectivos testes condicionais ignorados entre os 25. A2A/AG-UI permanecem preview; retomada do supervisor em outro host segue na issue futura #134.

## Gates ainda abertos

| Gate | Estado |
|---|---|
| SQL `dt.complete_tasks` na base recém-migrada com 20 migrations | Concluído: schema/colunas EF, batches, rollback, concorrência e sessão passaram |
| Checker atualizado de links e índices após esta edição | Passou: 169 documentos, 936 links, 0 quebrados |
| Commit por contexto para alterações locais ainda sem commit | Pendente |
| Push dos commits finais e CI remoto do PR #152 | Pendente |
| Confirmar issues relacionadas no PR apenas quando critérios integrais estiverem comprovados | Pendente |

Nenhuma issue original deve ser fechada por subconjunto de critérios. O relatório separa implementação, validação local, integrações condicionais e CI remoto; não trata doubles ou skips como prova de serviços externos.
