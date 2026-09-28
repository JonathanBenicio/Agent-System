# AGENTS.md

## Core Commands & Setup

### Backend (.NET 10)
```bash
# Quick start
cp src/AgenticSystem.Api/appsettings.example.json src/AgenticSystem.Api/appsettings.json  # Configure API keys
dotnet restore
dotnet run --project src/AgenticSystem.Api --urls http://localhost:5001
dotnet test                                 # 688 unit tests, 80% coverage required

# Build & publish
dotnet build --configuration Release
dotnet publish --configuration Release
```

### Frontend (React + Vite)
```bash
cd frontend
npm install
npm run dev          # http://localhost:5173 (proxy to localhost:5001)
npm run build        # tsc + vite build
npm run lint         # ESLint
npm run cy:run       # Cypress E2E tests
```

### CI Commands
```bash
# Backend CI (uses .NET 10 - aligned with code targets net10.0)
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release \
  --logger "trx;LogFileName=test-results.trx" \
  --collect:"XPlat Code Coverage"

# Coverage threshold enforcement (80% minimum)
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

## Documentation workflow

Follow [workflow](conductor/workflow.md) and [templates](templates/README.md). Current operational contracts: [backend hub](docs/backend/README.md). Record decision/implementation/validation separately and report gaps honestly.

## Architecture Boundaries

### Single Source of Truth
- **Architecture**: `docs/architecture/backend-architecture-explained.md` (MAF 1.9.0 framework-first)
- **Product Boundary**: `.github/copilot-instructions.md` (Core vs Lab governance)

### 📜 Governança de Documentação (Regra de Ouro)
Toda nova funcionalidade estratégica deve seguir rigorosamente esta ordem:
1. **GitHub Issue**: Registro da necessidade. **Obrigatório atualizar a descrição da Issue com links para o ADR, Story e Plan assim que criados.**
2. **ADR (Architectural Decision Record)**: Definição de padrões em `docs/architecture/adr/`.
3. **User Story**: Critérios de aceite em `docs/USER-STORIES.md`.
4. **Implementation Plan**: Roteiro técnico em `docs/plan/`.
5. **Rastreabilidade**: Commits vinculados à issue (ex: `feat: ... Closes #ID`).
6. **Sincronização de Índices**: Atualizar `README.md`, `INDEX.md` e `CONSOLIDATED_DOCS.md`.

Consulte o [Master Roadmap Q2 2026](docs/plan/master-roadmap-2026.md) para prioridades.

### Package Responsibilities
- **AgenticSystem.Api**: Web API + SignalR hubs (`/hubs/chat`, `/hubs/gateway`, `/hubs/external-agent`, `/hubs/workflow`, `/hubs/onnx`)
- **AgenticSystem.Core**: Business logic, agents, workflows, tenant isolation (MAF native)
- **AgenticSystem.Infrastructure**: External services (LLM, vector stores, MCP, gateway, PostgreSQL persistence)
- **AgenticSystem.Tests**: Unit tests (xUnit + FluentAssertions + NSubstitute)

## Runtime Quirks & Constraints

### .NET Version Alignment
- **Code targets**: .NET 10.0 (all csproj files)
- **CI uses**: .NET 10.0 (workflows/ci.yml) - **CI is fully aligned**
- **SDK**: Use .NET 10 locally and in CI

### Auto-Migrations & Startup
```csharp
// EF Core migrations auto-run on startup (Program.cs)
await dbContext.Database.MigrateAsync();
```
- No manual migration execution needed
- PostgreSQL connection required for production
- **To generate new migrations in the correct folder, use:**
  ```bash
  dotnet ef migrations add <Name> --project src/AgenticSystem.Infrastructure --startup-project src/AgenticSystem.Api --output-dir Persistence/Migrations
  ```

### Authentication & Authorization
- **MultiAuth**: API Key OR JWT via `PolicyScheme`
- **Tenant Context**: `TenantMiddleware` extracts tenant from `X-Tenant-Id` header (priority) or JWT `tenant_id` claim (fallback). Controller routes with AuthorizeAttribute reject unknown tenants; Admin override and hub fallback require validation (docs/backend/access-tenants.md).
- **Rate Limiting**: Per-tenant sliding window (`/api/chat`: 30 req/min default)

### Configuration Sections
```json
{
  "AgenticSystem": {
    "Ollama": { "Enabled": true, "Priority": 1 },      // Default provider
    "OpenAI": { "Enabled": false, "Priority": 10 },    // Disabled by default
    "Gateway": { "DefaultDailyBudget": 50.00 },
    "Memory": { "VectorStoreType": "InMemory" },       // Dev fallback
    "LocalExecution": { "StorageMode": "PostgreSQL" }  // Production
  }
}
```

### Service Dependencies
- **PostgreSQL**: Required for production (pgvector support)
- **Ollama**: Optional local LLM (docker-compose.yml includes it)
- **SignalR**: Real-time chat, gateway monitoring, and workflow progress streaming

## Testing & Quality

### Test Commands
```bash
# Backend
dotnet test                              # Full suite (688 tests)
dotnet test --filter "Name~Tests"       # Specific tests

# Frontend
cd frontend && npm run cy:run            # E2E tests
cd frontend && npm run lint              # Code linting

# Coverage
# CI enforces 80% minimum coverage threshold
```

### Test Project Structure
- References all 3 source projects
- Uses `InternalsVisibleTo` for Infrastructure tests
- Test data: `tests/k6/load-test.js` for load testing

### Build Order Validation
```bash
# Required command sequence (lint/build/E2E)
npm run lint && npm run build && npm run cy:run
# Frontend fails if any step fails (zero tolerance)
```

## Development Conventions

### Code Style
- **Backend**: Follow existing patterns in `src/`
- **Frontend**: SPA only (no Next.js), use `cn()` for Tailwind class merging
- **Agent Code**: MAF native via `.AsAIFunction()` in Core project

### Error Handling
- **Correlation ID**: Added to error responses (`X-Correlation-Id` header)
- **JSON Corruption**: Safe handling in `GetAsync`/`ReadSessionsAsync`
- **Circuit Breaker**: Pure C# implementation with auto-failover

### Protocol Support
- **A2A**: `/a2a` endpoint (enabled by default)
- **AG-UI**: `/agui` endpoint (enabled by default)  
- **MCP**: `/mcp` is not mapped in the baseline; MCP client plugins are separate
- **OpenAI Compatible**: Enabled for protocol hosting

### Observability
- **Logging**: Serilog with structured events
- **Telemetry**: OpenTelemetry + Application Insights
- **Real-time**: SignalR hubs for chat, gateway, and workflow events

## Deployment & Operations

### Docker Commands
```bash
# Build
docker build -t agentic-system .

# Run with dependencies (API + PostgreSQL + Ollama)
docker-compose up -d

# Health checks
curl http://localhost:8080/health
```

### Environment Requirements
- **.NET 10 SDK** (local and CI aligned)
- **Node.js 20+** for frontend
- **PostgreSQL 16+** with pgvector extension (production)
- **Ollama** (optional, for local LLM)

### Configuration Management
- **Secrets**: User secrets in API project (`UserSecretsId`)
- **Environment**: `ASPNETCORE_ENVIRONMENT` controls behavior
- **CORS**: Development allows all origins, production requires configured origins

## Implemented Features (Recent)

### RAG Metrics
- `GET /api/document/stats` — returns total chunks, search count (24h), ONNX runtime status

### Knowledge Rooms
- Full CRUD via `GET/POST/PUT/DELETE /api/knowledge/rooms`
- Permission-based access control (`KnowledgeRoomPermissionEntity`)
- Tenant-isolated with `X-Tenant-Id` header

### Workflow Builder
- Definition CRUD via `GET/POST/DELETE /api/workflow/definitions`
- Execution lifecycle via `POST /api/workflow/executions/start/{id}`, `GET /api/workflow/executions/{id}`, `POST /api/workflow/executions/{id}/cancel`
- SignalR real-time progress streaming via `/hubs/workflow`
- Step types: Action, Decision, Wait, Approval, Parallel, Subworkflow
- Event broadcasting: `IWorkflowEventBroadcaster` → `SignalRWorkflowEventBroadcaster`

### Tenant Isolation
- `X-Tenant-Id` header takes precedence over JWT `tenant_id` claim
- Unknown tenants rejected on controller routes with AuthorizeAttribute; hub fallback differs (see docs/backend/access-tenants.md)
- Global EF Core query filters apply `TenantId` to all `ITenantEntity` types
