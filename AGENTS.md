# AGENTS.md

## Core Commands & Setup

### Backend (.NET 10)
```bash
# Quick start
cp appsettings.example.json appsettings.json  # Configure API keys
dotnet restore
dotnet run --project src/AgenticSystem.Api   # https://localhost:5001
dotnet test                                 # 344 unit tests, 80% coverage required

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
# Backend CI (uses .NET 8 - note: code targets net10.0)
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release \
  --logger "trx;LogFileName=test-results.trx" \
  --collect:"XPlat Code Coverage"

# Coverage threshold enforcement (80% minimum)
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

## Architecture Boundaries

### Single Source of Truth
- **Architecture**: `docs/architecture/backend-architecture-explained.md` (MAF 1.5.0 framework-first)
- **Product Boundary**: `.github/copilot-instructions.md` (Core vs Lab governance)

### Package Responsibilities
- **AgenticSystem.Api**: Web API + SignalR hubs (`/hubs/chat`, `/hubs/gateway`, `/hubs/external-agent`)
- **AgenticSystem.Core**: Business logic, agents, workflows (MAF native)
- **AgenticSystem.Infrastructure**: External services (LLM, vector stores, MCP, gateway)
- **AgenticSystem.Tests**: Unit tests (xUnit + FluentAssertions + NSubstitute)

## Runtime Quirks & Constraints

### .NET Version Mismatch
- **Code targets**: .NET 10.0 (all csproj files)
- **CI uses**: .NET 8.0 (workflows/ci.yml) - **CI is outdated**
- **SDK**: Use .NET 10 locally, ignore CI version mismatch

### Auto-Migrations & Startup
```csharp
// EF Core migrations auto-run on startup (Program.cs:399-415)
await dbContext.Database.MigrateAsync();
```
- No manual migration execution needed
- PostgreSQL connection required for production

### Authentication & Authorization
- **MultiAuth**: API Key OR JWT via `PolicyScheme`
- **Tenant Context**: Middleware isolates multi-tenant data
- **Rate Limiting**: Per-tenant sliding window (`/api/chat`: 30 req/min default)

### Configuration Sections
```json
{
  "AgenticSystem": {
    "Ollama": { "Enabled": true, "Priority": 1 },      // Default provider
    "OpenAI": { "Enabled": false, "Priority": 10 },    // Disabled by default
    "Gateway": { "DefaultDailyBudget": 50.00 },
    "Memory": { "VectorStoreType": "InMemory" }      // Dev fallback
  }
}
```

### Service Dependencies
- **PostgreSQL**: Required for production (pgvector support)
- **Ollama**: Optional local LLM (docker-compose.yml includes it)
- **SignalR**: Real-time chat and gateway monitoring

## Testing & Quality

### Test Commands
```bash
# Backend
dotnet test                              # Full suite
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
- **MCP**: `/mcp` endpoint (commented out, requires auth)
- **OpenAI Compatible**: Enabled for protocol hosting

### Observability
- **Logging**: Serilog with structured events
- **Telemetry**: OpenTelemetry + Application Insights
- **Real-time**: SignalR hubs for gateway events

## Deployment & Operations

### Docker Commands
```bash
# Build
docker build -t agentic-system .

# Run with dependencies
docker-compose up -d

# Health checks
curl http://localhost:8080/health
```

### Environment Requirements
- **.NET 10 SDK** (CI is outdated, use local version)
- **Node.js 20+** for frontend
- **PostgreSQL 16+** with pgvector extension (production)
- **Ollama** (optional, for local LLM)

### Configuration Management
- **Secrets**: User secrets in API project (`UserSecretsId`)
- **Environment**: `ASPNETCORE_ENVIRONMENT` controls behavior
- **CORS**: Development allows all origins, production requires configured origins