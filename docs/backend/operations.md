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
dotnet build src/AgenticSystem.Api --configuration Release --no-restore
powershell -File tests/backend-validation/start-api.ps1
# Em outro terminal:
node tests/backend-validation/core-diagnostics.mjs
node tests/backend-validation/session-diagnostics.mjs
dotnet run --project tests/backend-validation/BackendDiagnostics.csproj --configuration Release
dotnet run --project tests/backend-validation/BackendDiagnostics.csproj --configuration Release -- --openapi
powershell -File tests/backend-validation/stop-api.ps1
# Reiniciar start-api.ps1 em terminal próprio, depois:
node tests/backend-validation/session-diagnostics.mjs --after-restart
powershell -File tests/backend-validation/stop-api.ps1
docker compose -f tests/backend-validation/compose.yml stop
```
Portas exclusivas 55432/11435/5188, DB backend_validation e volumes do projeto agent-system-doc-validation. Scripts recusam alvo HTTP externo; dados e credenciais sintéticos. Parada conserva volumes/evidências; remoção exige selecionar somente este projeto. Se API falhar startup, registrar causa antes de afirmar que cenários passaram.
Resultados brutos em tests/TestResults/backend-documentation, resumo sanitizado em docs/backend/validation.

## Verificações
```powershell
node scripts/backend-contract-inventory.mjs
node scripts/check-documentation-links.mjs
dotnet test tests/AgenticSystem.Tests --configuration Release --no-restore --collect:"XPlat Code Coverage"
```
Cobertura mínima 80%; registrar medida real e impedimento no PR. Os [templates](../../templates/README.md) exigem rastreabilidade e resultados reais.

O [relatório desta baseline](validation/2026-09-28.md) pode ser consolidado com `node scripts/build-backend-validation-report.mjs` depois de executar os diagnósticos e cobertura nos caminhos indicados. Baseline/versões do consolidado são desta execução: revisar esses metadados ao validar outra revisão. Os resultados brutos são exigidos; o script não inventa aprovações.
