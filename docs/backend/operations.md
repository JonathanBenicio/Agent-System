# Execução e operação do backend
.NET 10 SDK; PostgreSQL/pgvector para persistência real; Ollama para validação local. Versões de packages vêm dos csproj (MAF 1.9.0; hosting/DurableTask incluem preview). Não concluir estabilidade a partir da versão.

## Desenvolvimento
A configuração de exemplo fica em src/AgenticSystem.Api/appsettings.example.json, não na raiz.
```powershell
Copy-Item src/AgenticSystem.Api/appsettings.example.json src/AgenticSystem.Api/appsettings.json
dotnet restore
dotnet run --project src/AgenticSystem.Api --urls http://localhost:5001
```
Configurar somente credenciais locais apropriadas; não commitar secrets nem sobrescrever appsettings existente. URL é controlada pelo host, não há garantia de HTTPS/porta pelo comando sem --urls. JWT secret e Encryption:Key obrigatórios fora de Development; CORS AllowedOrigins obrigatório em produção. API key precisa estar habilitada no banco; bootstrap legacy AdminApiKey só cria chave quando banco inicial está vazio.

StorageMode PostgreSQL e VectorStoreType PostgreSQL selecionam persistência; InMemory é diagnóstico/desenvolvimento, não prova de durabilidade. Startup tenta migrações e bootstrap, mas catch loga falha e continua. /health é liveness; confirmar migrations e acesso a tabelas separadamente.

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
Portas exclusivas 55432/11435/5188, DB backend_validation e volumes do projeto agent-system-doc-validation. Scripts recusam alvo HTTP externo; dados e credenciais sintéticos. Parada conserva volumes/evidências; remoção exige selecionar somente este projeto. Se API falhar startup, registrar causa antes de afirmar que cenários passaram.
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
