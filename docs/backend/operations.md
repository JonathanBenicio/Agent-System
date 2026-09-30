# Execução e operação do backend
.NET 10 SDK; PostgreSQL/pgvector para persistência real; Ollama para validação local. O upgrade em andamento usa core MAF 1.22.0, M.E.AI 10.10.0 e hosting A2A/AG-UI 1.22.0-preview.260918.1; Microsoft.Agents.AI.DurableTask permanece em 1.16.0-preview.260922.1 porque não há versão 1.22 publicada. Isso demonstra restore/build, não certifica execução DurableTask/PostgreSQL nem os protocolos preview. Veja a [matriz de compatibilidade](../plan/maf-122-compatibility-review.md) e o [plano #120](../plan/maf-122-protocols-gateway.md).

## Desenvolvimento
A configuração de exemplo fica em src/AgenticSystem.Api/appsettings.example.json, não na raiz.
```powershell
Copy-Item src/AgenticSystem.Api/appsettings.example.json src/AgenticSystem.Api/appsettings.json
dotnet restore
dotnet run --project src/AgenticSystem.Api --urls http://localhost:5001
```
Configurar somente credenciais locais apropriadas; não commitar secrets nem sobrescrever appsettings existente. URL é controlada pelo host, não há garantia de HTTPS/porta pelo comando sem --urls. JWT secret e Encryption:Key obrigatórios fora de Development; CORS AllowedOrigins obrigatório em produção. API key precisa estar habilitada no banco. Em banco sem tenants, `AgenticSystem:AdminApiKey` é obrigatória para o bootstrap inicial; configure `AgenticSystem__AdminApiKey` no ambiente. Sem ela, o startup lança `MissingTenantBootstrapConfigurationException` e encerra a inicialização. Com tenants já provisionados, essa chave não é exigida pelo bootstrap.

StorageMode PostgreSQL e VectorStoreType PostgreSQL selecionam persistência; InMemory é diagnóstico/desenvolvimento, não prova de durabilidade. Startup executa migrações e bootstrap. A falta de `AdminApiKey` quando o banco ainda não tem tenants é fatal; outras falhas de migração/bootstrap continuam sendo registradas e podem não interromper a inicialização. `/health` é liveness; confirmar migrations e acesso a tabelas separadamente.

## Providers LLM e Gateway

`/api/admin/llm` é restrito a Platform Admin. Modelos, habilitação, prioridade, default e chaves globais ficam em `platform_configs`; segredos são cifrados, e `platform_config_audits` registra ator e hashes sem gravar segredo. Configure `AgenticSystem:Encryption:Key` fora de Development antes de habilitar armazenamento persistido. BYOK continua em credenciais/configuração tenant-scoped e mantém quota por tenant; esses valores não são promovidos ao store global. Chamadas com chave global passam pelo Gateway; chamadas BYOK preservam o isolamento do tenant.

## Hyperlight CodeAct (Lab)

Hyperlight usa o pacote preview `Microsoft.Agents.AI.Hyperlight` e permanece desligado por padrão. O tool só é registrado quando `AgenticSystem:Hyperlight:Enabled=true` e `ASPNETCORE_ENVIRONMENT=Lab`. A integração atual executa JavaScript; Python/C# não são anunciados como suportados. Não configura montagens de filesystem nem allowlist de rede. Com a flag desligada ou fora de Lab, o tool não é registrado; falhas reais da sandbox retornam erro, sem saída simulada.

## FIDES e varredura de mídia

FIDES processa mídia localmente com TesseractOCR 5.5.2 e PDFtoImage 5.4.0. A imagem final é enviada apenas depois da redaction; PDFs são reconstituídos como páginas rasterizadas para retirar o texto oculto original. O container instala os modelos `eng` e `por`; fora do container, configure `AgenticSystem:Fides:TessDataPath`. Sem os modelos, com confiança baixa ou ao exceder os limites, a requisição é bloqueada.

## Compose existente
API 8080, PostgreSQL host 5433, Ollama 11434. Compose ativa alguns providers externos e pede GPU NVIDIA; revisar ambiente antes de usar. Para diagnóstico deste plano, usar [compose isolado](../../tests/backend-validation/compose.yml), sem providers externos, sem GPU obrigatória.

## Diagnóstico isolado
```powershell
docker compose -f tests/backend-validation/compose.yml up -d
docker compose -f tests/backend-validation/compose.yml exec -T postgres psql -U validation -d backend_validation -c "CREATE EXTENSION IF NOT EXISTS vector;"
docker compose -f tests/backend-validation/compose.yml exec -T ollama ollama pull qwen2.5:0.5b
docker compose -f tests/backend-validation/compose.yml exec -T ollama ollama pull nomic-embed-text
dotnet build tests/backend-validation/BackendDiagnostics.csproj --configuration Release
powershell -File tests/backend-validation/start-api.ps1
# Em outro terminal:
node tests/backend-validation/core-diagnostics.mjs
dotnet run --project tests/backend-validation/BackendDiagnostics.csproj --configuration Release --no-build -- --session-fixture
node tests/backend-validation/session-diagnostics.mjs
dotnet run --project tests/backend-validation/BackendDiagnostics.csproj --configuration Release --no-build
dotnet run --project tests/backend-validation/BackendDiagnostics.csproj --configuration Release --no-build -- --openapi
powershell -File tests/backend-validation/stop-api.ps1
# Reiniciar start-api.ps1 em terminal próprio, depois:
node tests/backend-validation/session-diagnostics.mjs --after-restart
powershell -File tests/backend-validation/stop-api.ps1
docker compose -f tests/backend-validation/compose.yml stop
```
Portas exclusivas 55432/11435/5188, DB backend_validation e volumes do projeto Compose. Para execução isolada por tarefa, use um nome de projeto único, por exemplo `docker compose -p agent-system-backend-validation-<run-id> -f tests/backend-validation/compose.yml up -d postgres`; isso cria volumes próprios. Confira `docker compose ... ps` e o bind em `127.0.0.1` antes de migrations/testes. Scripts recusam alvo HTTP externo; dados e credenciais sintéticos. Parada conserva volumes/evidências; remoção exige selecionar somente o projeto desta execução. Se API falhar startup, registrar causa antes de afirmar que cenários passaram.

Testes de integração PostgreSQL podem ser executados no Compose isolado abaixo; nunca apontar esses testes para o banco principal do produto. Antes de `dotnet ef database update` ou dos testes, configurar **ambas** as variáveis para o mesmo alvo do Compose; a factory EF usa `AGENTIC_EF_CONNECTION` e não `ConnectionStrings__SessionStore`. O teste falha fechado se host/porta/database/usuário não forem `127.0.0.1:55432/backend_validation/validation`.
```powershell
$env:AGENTIC_EF_CONNECTION = 'Host=127.0.0.1;Port=55432;Database=backend_validation;Username=validation;Password=validation_local_only'
$env:AGENTIC_TEST_POSTGRES = $env:AGENTIC_EF_CONNECTION
dotnet ef database update --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --configuration Release
dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --configuration Release --filter FullyQualifiedName~PostgresPlatformConfigStoreIntegrationTests
```
Novas execuções de core/session gravam em tests/TestResults/backend-documentation/current, preservando os artefatos históricos na pasta pai. O after-restart exige o mesmo run/baseline e um snapshot anterior não vazio; compara IDs, papéis, conteúdo, ordem e timestamps. Não misturar fases de execuções diferentes. Resumos sanitizados ficam em docs/backend/validation.
O modo --session-fixture grava mensagens sintéticas conhecidas pelo PostgresSessionStore real no tenant criado por core-diagnostics. A prova de persistência é independente do chat/LLM; não declarar conversa bem-sucedida a partir dessa fixture. Compilar antes de iniciar a API e usar --no-build durante a execução evita DLLs bloqueadas no Windows. Os modos store/--openapi continuam escrevendo nos caminhos antigos: preservar/copiar a evidência original antes de reexecutá-los, pois o consolidado histórico recusará arquivos substituídos.

## Verificações
```powershell
node scripts/backend-contract-inventory.mjs
node scripts/check-documentation-links.mjs
dotnet test tests/AgenticSystem.Tests --configuration Release --no-restore --collect:"XPlat Code Coverage"
```
Cobertura mínima 80%; registrar medida real e impedimento no PR. Os [templates](../../templates/README.md) exigem rastreabilidade e resultados reais.

O [relatório histórico desta baseline](validation/2026-09-28.md) pode ser reproduzido com `node scripts/build-backend-validation-report.mjs` somente com os artefatos originais. O [manifesto](validation/2026-09-28-manifest.json) fixa seus hashes, caminho exato da cobertura, baseline e ambiente. O script lê Counters do TRX e métricas do Cobertura; recusa artefatos substituídos antes de escrever. Outra execução exige manifesto e relatório próprios; não atualizar o histórico com dados de current.

Verificar as regressões do harness com `node --test tests/backend-validation/evidence.test.mjs`. Esses testes controlados verificam as asserções e o parser; não certificam o produto. CHAT-02 exige HTTP 403/404. HUB-02/03 exigem HUB-01 aprovado e negação explícita HTTP 403 ou a mensagem de violação de tenant conhecida do filtro. Timeout, desconexão, erro de provider e erros genéricos falham; fechamento sem motivo de autorização nunca é apresentado como prova de isolamento.
