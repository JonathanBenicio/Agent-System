# Migration of ISkillManager, Tool Persistence, and Distributed Security

This plan outlines the steps required to transition critical state-management services from in-memory implementations to database-backed (PostgreSQL) implementations in `AgenticSystem`, enabling true multi-node horizontal scalability.

## Background & Motivation

1. **Migrar ISkillManager**: Replace `InMemorySkillManager` with `PostgresSkillManager` to dynamically read/write skills from `DbSkillEntity` in PostgreSQL.
2. **Persistência de Tools**: Introduce a `DbToolEntity` so MCP and custom tools are persisted across application restarts. Integrate this with a new `PostgresToolManager`.
3. **Segurança Distribuída**: Verify and ensure that `IPermissionService` and `IPolicyStore` are fully utilizing their PostgreSQL implementations (`PostgresPermissionService` and `PostgresPolicyStore`) to support a distributed API architecture.

## Proposed Changes

### Database Persistence & Entities

#### `DbToolEntity.cs`
- Create `DbToolEntity` implementing `ITenantEntity`.
- **Properties**: `Id`, `TenantId`, `Name`, `Description`, `Category`, `ConfigurationJson`, `CreatedAt`, `IsActive`.

#### `AgenticDbContext.cs`
- Add `public DbSet<DbToolEntity> AgentTools { get; set; }`.

---

### Core Services Migration

#### `PostgresSkillManager.cs`
- Implement `ISkillManager`.
- Use `IDbContextFactory<AgenticDbContext>` to perform CRUD operations on `DbSkillEntity`.
- Handle caching strategy (if any) or rely directly on the DB for multi-node consistency.

#### `PostgresToolManager.cs`
- Implement `IToolManager`.
- Query and persist tools using `DbToolEntity` instead of an in-memory dictionary.
- Ensure tool retrieval is tenant-isolated.

---

### Dependency Injection Updates

#### `ServiceCollectionExtensions.cs`
- Update `UseLocalExecutionStorageMode` and related setup extensions:
  - Register `PostgresSkillManager` when PostgreSQL is enabled (replacing `InMemorySkillManager`).
  - Register `PostgresToolManager` when PostgreSQL is enabled (replacing `InMemoryToolManager`).
  - Confirm `UsePostgresSecurityAndAudit` properly overrides `IPermissionService` and `IPolicyStore` with `PostgresPermissionService` and `PostgresPolicyStore`.

## Verification Plan

### Automated Tests
- Run `dotnet test` to ensure no existing tests break due to DI changes.
- Ensure `InMemorySkillManagerTests` continue to pass (as they test the in-memory fallback), and optionally write tests for the new Postgres providers using a Test Database.

### Manual Verification
- Start the API with Postgres configured.
- Register a custom tool and a skill, restart the application, and verify they are persisted and loaded correctly.
