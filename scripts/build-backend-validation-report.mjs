import { readFileSync, readdirSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
const root = resolve(import.meta.dirname, '..');
const raw = resolve(root, 'tests/TestResults/backend-documentation');
const output = resolve(root, 'docs/backend/validation');
mkdirSync(output, { recursive: true });
const read = name => JSON.parse(readFileSync(resolve(raw, name), 'utf8'));
const hash = file => createHash('sha256').update(readFileSync(file)).digest('hex');
const sources = ['core-results.json', 'store-results.json', 'session-before-restart.json', 'session-after-restart.json'];
const records = sources.flatMap(name => read(name).results.map(record => ({ ...record, artifact: name,
  result: record.detail.startsWith('dependency') ? 'not_executed' : record.result })));
const sourceInventory = JSON.parse(readFileSync(resolve(root, 'docs/backend/endpoint-inventory.json'), 'utf8'));
const openapi = read('openapi-contracts.json');
const normalize = route => route.toLowerCase().replaceAll('*', '');
const visible = sourceInventory.routes.filter(r => !r.openApiHidden && !r.condition.includes('DEBUG'));
const missing = visible.filter(r => !openapi.contracts.some(o => o.method === r.method && normalize(o.path) === normalize(r.path)));
if (missing.length || visible.length !== openapi.contracts.length) throw new Error('OpenAPI/source inventory mismatch');
const coverageDir = resolve(raw, 'coverage');
const coverageFolder = readdirSync(coverageDir, { withFileTypes: true }).find(e => e.isDirectory());
if (!coverageFolder) throw new Error('Missing real coverage artifact');
const coverageFile = resolve(coverageDir, coverageFolder.name, 'coverage.cobertura.xml');
const xml = readFileSync(coverageFile, 'utf8');
const rate = Number(xml.match(/<coverage[^>]*\bline-rate="([^"]+)"/)[1]);
const summary = records.reduce((total, r) => (total[r.result] = (total[r.result] || 0) + 1, total), {});
const dataset = {
  baseline: 'f8de7a6e3aa9d671a67ae60f2f52e0c1f80b3eae', date: '2026-09-28',
  environment: { dotnet: '10.0.201', node: '24.12.0', postgres: '16.15', pgvector: '0.8.6', ollama: '0.34.4',
    generationModel: 'qwen2.5:0.5b', embeddingModel: 'nomic-embed-text', embeddingDimensions: 768,
    apiEnvironment: 'Validation', ports: [5188, 55432, 11435], realServices: true, mockLlm: false },
  unitTests: { passed: 687, skipped: 1, failed: 0, total: 688 },
  coverage: { linePercent: Number((rate * 100).toFixed(2)), minimum: 80, passed: rate >= 0.8, sha256: hash(coverageFile) },
  inventory: { source: sourceInventory.routes.length, releaseOpenApi: openapi.contracts.length, unmatched: missing.length },
  artifactHashes: Object.fromEntries(sources.concat('openapi-contracts.json', 'openapi-release.json', 'baseline.trx').map(name => [name, hash(resolve(raw, name))])),
  summary, records
};
writeFileSync(resolve(output, '2026-09-28-results.json'), JSON.stringify(dataset, null, 2) + '\n');
const status = { passed: 'passou', failed: 'falhou', not_executed: 'não executado' };
const escape = text => String(text).replaceAll('|', '\\|').replace(/\s+/g, ' ').trim();
const rows = records.map(r => `| ${r.id} | ${escape(r.criterion)} | ${status[r.result] || r.result} | ${escape(r.detail)} |`).join('\n');
writeFileSync(resolve(output, '2026-09-28.md'), `# Validação do backend — 2026-09-28
Baseline: ${dataset.baseline}. Epic: [#110](https://github.com/JonathanBenicio/Agent-System/issues/110). [Plano](../../plan/backend-documentation-validation.md), [dados sanitizados](2026-09-28-results.json), [backlog](../backlog.md).

## Resultado e prontidão
Documentação e processo entregues. Diagnóstico integrado: **${summary.passed} passaram, ${summary.failed} falharam, ${summary.not_executed} não executados por dependência**, ${records.length} cenários. Isso não é certificação do produto: gaps P0 impedem declarar isolamento estável; chat funcional falhou. Hierarquia desejada permanece alvo de ADR-034.

Suíte existente: 687 aprovados, 1 ignorado, 0 falhas, total 688. Cobertura de linhas **${dataset.coverage.linePercent.toFixed(2).replace('.', ',')}%**, abaixo dos 80% exigidos; 12.545/56.029 linhas, branches 25,82%. Teste ignorado de SQL ANY não conta como aprovação; diagnóstico real reproduziu falha SQL.

Inventário: ${dataset.inventory.source} rotas de controladores; ${dataset.inventory.releaseOpenApi} no OpenAPI Release, sem divergências após normalizar casing/catch-all e excluir duas rotas DEBUG/STAGING e três actions IgnoreApi. Hubs, SSE e mappings de Program foram documentados separadamente. OpenAPI foi gerado com registrations MVC/Swagger da API, sem iniciar seu pipeline; isso verifica descrição/reflexão, não autorização de todas as actions.

## Ambiente
Windows; .NET SDK10.0.201, Node24.12.0. PostgreSQL16.15/pgvector0.8.6 e Ollama0.34.4 reais, imagens fixadas por digest no [compose](../../../tests/backend-validation/compose.yml). Modelos qwen2.5:0.5b (geração) e nomic-embed-text (768 dimensões). Sem mocks de LLM/PG e sem providers externos habilitados.
API Release em Validation na porta loopback5188; PG55432/Ollama11435; DB backend_validation e volumes do projeto agent-system-doc-validation. Configuração/identidades/chaves sintéticas; conteúdo-root isolado; Validation evita user secrets. JWT/Encryption/CORS configurados. Primeira tentativa de startup falhou por Encryption:Key ausente; corrigida apenas a configuração do harness, não produção. Em seguida seis migrations aplicadas e bootstrap registrado.

## Comandos executados
Na raiz do repositório:

\`\`\`powershell
dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --configuration Release --no-restore --logger "trx;LogFileName=baseline.trx" --results-directory tests/TestResults/backend-documentation
dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --configuration Release --no-build --no-restore --collect:"XPlat Code Coverage" --logger "trx;LogFileName=coverage.trx" --results-directory tests/TestResults/backend-documentation/coverage
node scripts/backend-contract-inventory.mjs
node tests/backend-validation/core-diagnostics.mjs
dotnet run --project tests/backend-validation/BackendDiagnostics.csproj --configuration Release
node tests/backend-validation/session-diagnostics.mjs
# Parada/início da API isolada; depois:
node tests/backend-validation/session-diagnostics.mjs --after-restart
dotnet run --project tests/backend-validation/BackendDiagnostics.csproj --configuration Release -- --openapi
node scripts/build-backend-validation-report.mjs
node scripts/check-documentation-links.mjs
\`\`\`
Setup, extensão vector, models, configuração e parada: [operação](../operations.md). Diagnósticos retornam exit1 quando encontram falha de produto; não são gates falsamente verdes. O harness teve ajustes de compilação/saída durante desenvolvimento; os cenários abaixo foram executados após essas correções.

## Cenários e evidências
| ID | Critério | Resultado | Observação |
|---|---|---|---|
${rows}

SESSION-01/CHAT-02 originais não executados: CHAT-01 não retornou sessionId válido. Um diagnóstico adicional leu sessões persistidas mesmo de conversas falhas e comprovou ownership, mensagens, título e persistência após restart. Isso não prova retomada de conversa bem-sucedida nem negação correta dentro do chat.

## Causas e alcance
- AUTH-05: role Admin literal no autenticador, override aceito; não se demonstrou leitura de conteúdo de terceiros nesse cenário. [#111](https://github.com/JonathanBenicio/Agent-System/issues/111).
- HUB-02/03: query troca tenant e aceita desconhecido; GetDashboard completou. Não inferir leitura de salas a partir desse resultado. [#112](https://github.com/JonathanBenicio/Agent-System/issues/112).
- STORE-02: SQLSTATE42703, coluna metadata_json não existe; schema possui metadata. STORE-03: lista de salas vazia devolveu dois documentos. Prova no store; seguir callers/ACL antes de afirmar exploração via HTTP. [#113](https://github.com/JonathanBenicio/Agent-System/issues/113).
- CHAT/SSE/HUB: log “Session isolation key is required but was not provided by the configured SessionIsolationKeyProvider”; keyed AgentSessionStore em FrameworkOrchestratorService.ExecuteAsync linha83. Ollama direto funcionou. HTTP200/success=false e eventos Error/SessionCompleted não significam sucesso funcional. [#114](https://github.com/JonathanBenicio/Agent-System/issues/114).
- QUOTA-02: oito incrementos concorrentes, duas falhas explícitas após retries; seis gravados. Não se provou perda silenciosa; resultado/timing pode variar. Reset persistido e bloqueio por tokens passaram; agendamento real de meia-noite, cache rollover e múltiplas APIs não testados. [#115](https://github.com/JonathanBenicio/Agent-System/issues/115).
- Logs adicionais: SQLSTATE23505, PK_agent_skills durante seeding de defaults por tenant; IDs globais em DbAgentSkillsSource. Não apontado como única causa do chat. [#116](https://github.com/JonathanBenicio/Agent-System/issues/116).

## Não comprovado ou ausente
Memberships múltiplas/concessões temporárias de suporte e enforcement uniforme são alvo ainda ausente, [#117](https://github.com/JonathanBenicio/Agent-System/issues/117). Matriz detalhada de ações/migração será decidida na implementação. Deep validation de workflows, ONNX, agendamentos, plugins, evaluation e protocolos externos ficou fora do núcleo; não apresentada como estável. RAG autorizado ponta a ponta no chat e purga física/vetorial completa não comprovados. Estatísticas ONNX são parcialmente inferidas/fixas.
O diagnóstico usou dois documentos para SQL/salas: caso de >=55 candidatos não foi executado porque a query não compilou; consta nos critérios de correção. Quotas foram verificadas com conexões independentes no mesmo processo, não deploy multiinstância.
Build/restore reportaram CS8600 preexistente e NU1903 transitive (Microsoft.OpenApi2.4.1, SQLitePCLRaw2.1.11); sem atualização de produção nesta entrega. Nenhum frontend alterado, logo lint/build/E2E frontend não executados.

## Integridade e reprodução
Hashes SHA256 dos resultados/TRX/OpenAPI e cobertura constam no JSON sanitizado. Artefatos brutos ficam em tests/TestResults/backend-documentation, ignorados pelo Git; logs não são publicados. Fontes, configuração isolada e scripts estão versionados. Copiar exemplo/validar JSON e portas não equivale a Quick Start completo com configuração particular do usuário. Bootstrap/health/migrations reais foram verificados no setup isolado.
Parada restringe-se ao listener5188 com command line da DLL conhecida e ao projeto compose de validação; conserva volumes para análise. Merge/deploy e correções de produção não executados.
`);
writeFileSync(resolve(root, 'docs/backend/openapi-release.json'), readFileSync(resolve(raw, 'openapi-release.json')));
const schema = read('openapi-release.json');
const table = Object.entries(schema.components?.schemas || {}).map(([name, model]) => `### ${name}\n\n| Campo | Tipo/schema | Obrigatório no OpenAPI |\n|---|---|---|\n` + Object.entries(model.properties || {}).map(([field, spec]) => `| ${field} | ${escape(spec.$ref?.split('/').at(-1) || (spec.type === 'array' ? 'array of ' + (spec.items?.$ref?.split('/').at(-1) || spec.items?.type || 'object') : spec.type || 'object'))}${spec.enum ? ': ' + spec.enum.join(', ') : ''} | ${model.required?.includes(field) ? 'sim' : 'não'} |`).join('\n') + '\n').join('\n');
writeFileSync(resolve(root, 'docs/backend/request-schemas.md'), '# Schemas dos contratos MVC\n\nGerado do [OpenAPI Release](openapi-release.json) pelas registrations de produção. Obrigatoriedade/binding reflete o OpenAPI; validações de negócio no controller/store podem ser mais restritas. Consultar [núcleo](api-core.md), [inventário](endpoint-inventory.md) e fontes. Swagger declara segurança global ApiKey, mas o runtime aceita também JWT e há actions com auth própria: usar [guia de acesso](access-tenants.md), não inferir política só do spec. Este schema não abrange hubs ou protocolos de bibliotecas.\n\n' + table);
console.log(JSON.stringify({ summary, coverage: dataset.coverage.linePercent, inventory: dataset.inventory }));
