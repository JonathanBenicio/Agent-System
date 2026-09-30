# Backend Architectural Audit & Technical Diagnosis Report

**Date:** June 4, 2026\
**Status:** Completed\
**Objective:** Provide a comprehensive architectural audit of the AgenticSystem backend, evaluate alignment with the Q2 2026 Roadmap, analyze Dependency Injection (DI) and coupling in the Microsoft Agent Framework (MAF) integration, assess test suite compliance, evaluate the Microsoft Agent Framework 1.9.0 upgrade path, and define a clear Action Plan with recommended ADRs.

---

> Historical audit of the June 4 baseline. Current implementation and verification results are recorded in [the September 28 review](pending-changes-review-2026-09-28.md). The release analysis below describes the upgrade planned at that time.

## 1. Executive Summary

This audit presents a technical evaluation of the AgenticSystem backend codebase. While the core agentic capabilities and protocol endpoints are robust, critical architectural gaps have been identified in database-level vector filtering, tenant quota persistence, and Dependency Injection lifecycles. Furthermore, specific bugs in Quartz cron normalization and unit test database context lifecycles have been diagnosed.

Additionally, this audit evaluates the upgrade path from Microsoft Agent Framework (MAF) version 1.6.2 to the newly released version 1.9.0, identifying critical stabilization benefits for workflows, AGUI hosting, and DI configuration.

Incorporating Challenger 1 and Challenger 2's critical feedback, this report defines corrected refactoring recommendations to secure tenant isolation boundaries, resolve telemetry corruption, ensure Quartz scheduler reliability, prevent database isolation in unit tests, and resolve performance bottlenecks in quota verification. Implementing these recommendations will transition the platform from an in-memory prototype status to a secure, multi-tenant, high-performance enterprise agent system.

---

## 2. Q2 2026 Roadmap Alignment (Track Audit)

An audit of the four primary roadmap tracks reveals varying levels of completeness, highlighting significant implementation and documentation gaps.

### Track 1: Specialized Context (Rooms / RAG)
*   **Status:** Partially Implemented.
*   **Implementation Gaps:** In `PostgresVectorStore.SearchWithFiltersAsync`, filtering by `room_ids` is executed *in-memory* after fetching the top 50 semantic candidates. This represents a severe correctness and security risk: if the top 50 candidates do not contain the target room's documents, they will be discarded, resulting in zero retrieved results even if authorized documents exist.
*   **Documentation Gaps:** Existing architectural documentation does not highlight this in-memory limitation, incorrectly implying that metadata filtering is handled natively at the database level.
*   **Recommendation:** Move the room filtering logic directly into the PostgreSQL query (utilizing pgvector metadata filtering) to enforce safety and correctness before semantic scoring.

### Track 2: FinOps & Observability Hub
*   **Status:** Mostly Implemented.
*   **Implementation Gaps:** Cost calculation (`TokenAuditService.CalculateCostAsync`) and real-time streaming via SignalR (`GatewayHubPublisher`) are functional. However, tenant quotas and daily budgets are maintained strictly in-memory in the `QuotaEnforcer` using `ConcurrentDictionary` objects. These limits reset on application restart. The planned `ProactiveQuotaManager` database persistence is missing.
*   **Challenger 1 Critical Feedback & Performance Trade-off:**
    *   Retrieving quotas directly from the database on every single request presents a significant read/connection bottleneck under high load.
    *   *Recommendation:* Implement a short-TTL memory caching layer (e.g., using `IMemoryCache`) over the PostgreSQL backing for `QuotaEnforcer` to maintain high performance while ensuring quota limits are synchronized and persistent across application recycles.
*   **Documentation Gaps:** The planned `ProactiveQuotaManager` database persistence is documented as active in some files, leading to a mismatch between documented architecture and actual runtime state.

### Track 3: Protocol Interoperability (A2A & AgUI)
*   **Status:** Fully Implemented.
*   **Implementation Gaps:** None. The endpoints are correctly mapped in `Program.cs`.
*   **Design & Architecture:** The interaction between Singletons and Transient scopes is handled safely using `ScopedAgentProxy` to resolve the `Orchestrator` on a per-request basis.
*   **Recommendation:** Maintain the current implementation while adding regression integration tests for the protocol endpoints.

### Track 4: Continuous Evaluation Suite
*   **Status:** Mostly Implemented.
*   **Implementation Gaps:** Heuristic and AI evaluations are operational under `RuntimeEvaluatorService` via `Microsoft.Extensions.AI.Evaluation`. Regressions are successfully detected and persisted. However, CRUD REST endpoints for managing Golden Sets are missing.
*   **Documentation Gaps:** The `docs/planejamento/Agent_Runtime_State_Machine.md` and related docs lack API endpoints specification for Golden Sets.
*   **Recommendation:** Design and implement a standard REST controller for Golden Set CRUD operations.

---

## 3. Dependency Injection and Coupling in MAF

The integration with the Microsoft Agent Framework (MAF) displays four critical architectural flaws that violate Dependency Injection best practices and introduce performance, reliability, and security risks.

### 1. Redundant Agent Construction (Double Build)
*   **Issue:** `FrameworkOrchestratorService` eager-resolves `OrchestratorContext` (triggering Build #1) and then calls `BuildHandoffWorkflowAsync` on `OrchestratorHostBuilder` (triggering Build #2).
*   **Consequence:** Double allocation and initialization overhead for agents, leading to degraded startup performance and redundant configuration logging.

### 2. Sync-over-Async Resolution
*   **Issue:** `OrchestratorContextFactory` forces synchronous blocking of async operations using `.GetAwaiter().GetResult()` during DI registration in `ServiceCollectionExtensions.cs`.
*   **Consequence:** Risk of thread pool starvation under high concurrent load, violating modern async/await patterns in ASP.NET Core.

### 3. Orphan Specialist Bindings
*   **Issue:** `OrchestratorContext` hardcodes `SpecialistBindings` to an empty array `[]`. However, the matching and routing logic is still actively run in `FrameworkOrchestratorService`. Because this collection is always empty, the matching loops never succeed and `calledBinding` resolves to `null`.
*   **Consequence:** This prevents the system from mapping the actual executed specialist agent and capturing metadata such as its specific Tier, leading to routing telemetry and agent tier metadata corruption.
*   **Refined Recommendation (Challenger 2 Feedback):** Recommend populating `SpecialistBindings` in `OrchestratorContextFactory` with actual specialist bindings retrieved from the registered agents instead of hardcoding `[]`. This restores telemetry accuracy and prevents metadata corruption.

### 4. Captive Provider Mismatch
*   **Issue:** `OrchestratorHostBuilder` is registered as a Singleton in `ServiceCollectionExtensions.cs` but captures the root `IServiceProvider`, forcing request-scoped dynamic resolutions to query the root provider.
*   **Consequence:** Captive dependency issues. Transient or scoped services resolved through a singleton builder cannot access request-scoped resources like HTTP context or tenant context, causing runtime failures or leaking tenant context under load.
*   **Refined Recommendation & DI Captive Dependency Validation (Challenger 1 & 2 Feedback):**
    *   Register `OrchestratorHostBuilder` as Scoped in `ServiceCollectionExtensions.cs` instead of Singleton. This secures scope boundaries, guarantees strict tenant context resolutions, and prevents memory leaks or cross-tenant data access.
    *   *Captive Dependency Validation Warning:* Registering `OrchestratorHostBuilder` as Scoped *necessitates* registering `OrchestratorContextFactory` as Scoped as well in `ServiceCollectionExtensions.cs`. If `OrchestratorContextFactory` remains registered as a Singleton while referencing a Scoped host builder, ASP.NET Core DI startup validation will fail and throw a captive dependency startup exception.

---

## 4. Microsoft Agent Framework (MAF) Release-by-Release Delta Comparison (1.6.2 to 1.9.0)

The system currently references packages from version `1.6.2` (and `1.6.2-preview.260521.1` for AGUI/A2A/DurableTask). The upgrade path to `1.9.0` introduces the following delta:

### 1. Version 1.7.0 Delta
*   **Long-running task support for client MCP tools:** Resolves timeout issues when executing complex client MCP tools that take longer than standard request windows.
*   **AGUI parallel tool call rendering fixes:** Corrects translation layer UI glitches when multiple tool calls render concurrently in the AgUI client interface.
*   **MessageId tracking:** Introduces `MessageId` tracking in `A2AAgent` for granular trace mapping and correlation of message streams.

### 2. Version 1.8.0 Delta
*   **BREAKING CHANGE - Code-Gen Removal:** Removal of code generation support in declarative workflows.
    *   *Impact on Workflow Builder Engine:* We must audit our Workflow Builder Engine to ensure it does not generate or compile C# workflow files dynamically; declarative workflow definitions must remain strictly JSON-based.
*   **A2AAgentSession updates:** Support for `reference_task_ids` and `input-required` protocol flags to enable human-in-the-loop workflows.
*   **MCP Skills:** Support for MCP-based skills in the standard `skill-md` markdown format.
*   **Iteration Persistence:** Checkpoint persistence of `ForeachExecutor` iteration state for durable workflows, enabling resilient resumption from the last processed item during loops.

### 3. Version 1.9.0 Delta
*   **Workflow Outputs Overhaul:** Overhauls outputs, supporting tagging and filtering of agent outputs for granular output validation.
*   **Removal of [Experimental] Tag:** Promotes .NET Orchestrations to stable, moving them out of experimental status.
*   **HarnessAgent DI Signatures:** Requires injection of `ILoggerFactory` and `IServiceProvider` in `HarnessAgent` constructor, eliminating manual dependency instantiation hacks and solving captive provider issues.

---

## 5. Test Suite Compliance and Bug Diagnosis

### Compliance Review
The backend test suite is highly compliant with the requirements in `AGENTS.md`:
*   **Frameworks:** Built on `.NET 10.0`, using `xUnit` for unit/integration tests, `FluentAssertions` for assertions, and `NSubstitute` for mocking.
*   **Patterns:** The AAA (Arrange-Act-Assert) pattern is consistently applied.
*   **Coverage:** The required threshold is 80%; it was not established by the original static audit. The verification on September 28, 2026 measured 22.39% line coverage (12,545 of 56,029 lines).

### Diagnosis of Unit Test Failures & Refined Resolutions

#### 1. MCPPluginController - Database Disposal Failure
*   **Symptom:** Unit tests fail due to an `ObjectDisposedException` when accessing the database context.
*   **Root Cause:** A shared instance of `AgenticDbContext` is registered and disposed of prematurely during test teardown before all asynchronous controller operations are completed.
*   **Challenger 2 Critical Feedback & Refined Resolution:**
    *   Refactoring unit tests to utilize `ContextCreator` to instantiate fresh contexts must remove `.EnableServiceProviderCaching(false)` (or share a single `InMemoryDatabaseRoot` instance).
    *   *Critical Side-Effect:* Disabling service provider caching (`.EnableServiceProviderCaching(false)`) forces EF Core to build a new internal service provider for every DbContext instance. This isolates in-memory database instances per DbContext, meaning data seeded in one context is invisible to queries executed in a separate context.
    *   *Recommendation:* Remove the `.EnableServiceProviderCaching(false)` setting, or explicitly share a single `InMemoryDatabaseRoot` instance across the DbContext instances to prevent isolated database stores during unit test execution.

#### 2. ScheduledTaskManager - Cron Normalization Failure
*   **Symptom:** Jobs with standard 5-part cron expressions fail to schedule or execute with a `null` `NextRunAt` timestamp.
*   **Root Cause 1:** The `NormalizeCronExpression` method ignores the 5th field (Day-of-Week) when converting standard 5-part cron expressions into the Quartz-compatible 6-part format.
*   **Root Cause 2:** Formatting exceptions thrown during Quartz cron parsing are silently swallowed by an empty catch block inside `ScheduledTaskManager`, returning a `null` next execution time without warning.
*   **Challenger 2 Critical Feedback & Refined Resolution:**
    *   *Quartz cron normalization:* Shift Linux Day-of-Week (DOW) 0-6 to Quartz 1-7 using the formula `(day % 7) + 1` (mapping Linux 0/7 Sunday to Quartz 1, Linux 1 Monday to Quartz 2, etc.) to prevent off-by-one shifts or Quartz ParseExceptions.
    *   *Validation and Error Handling:* Raise explicit validation errors to the caller on parsing failure rather than silently swallowing exceptions and setting `NextRunAt` to null, preventing silent task starvation.

---

## 6. Action Plan & Architectural Recommendations Summary

To address the findings of this audit, the following concrete actions must be taken:

### Technical Refactoring Items

1.  **Refactor Vector Store Filters:**
    *   Modify `PostgresVectorStore.SearchWithFiltersAsync` to pass metadata filters (`room_ids`) directly to the SQL query.
    *   Ensure the PGVector operator executes filtering prior to or during the semantic distance calculations.

2.  **Implement Database-Backed Tenant Quotas with Memory Cache:**
    *   Define a database schema for tenant quotas (`TenantQuota` table) linked to the tenant context.
    *   Refactor `QuotaEnforcer` to fetch quotas from PostgreSQL instead of in-memory dictionaries.
    *   Implement a short-TTL memory caching layer (e.g., `IMemoryCache`) to prevent database read/connection bottlenecks on every request.

3.  **Implement Golden Set CRUD APIs:**
    *   Create `GoldenSetController` with GET, POST, PUT, and DELETE endpoints to manage evaluation datasets.
    *   Integrate endpoints with `RuntimeEvaluatorService` to allow dynamic dataset updates.

4.  **Resolve Quartz Cron Normalization:**
    *   Correct the cron field mapping in `NormalizeCronExpression` by using `(day % 7) + 1` for Day-of-Week translation.
    *   Throw explicit validation exceptions on cron parsing failure to prevent silent task starvation.

5.  **Enforce Database-Factory Lifetime Scopes in Tests:**
    *   Adopt `IDbContextFactory<AgenticDbContext>` for unit and integration tests involving shared db context instances.
    *   Ensure `MCPPluginController` unit tests do not use `.EnableServiceProviderCaching(false)` without a shared `InMemoryDatabaseRoot`.

6.  **Microsoft Agent Framework 1.9.0 Upgrade:**
    *   Update all project references targeting `Microsoft.Agents.*` packages to version `1.9.0`.
    *   Transition the declarative workflows code away from experimental decorators and APIs.
    *   Refactor the `HarnessAgent` instantiation pipelines to pass `IServiceProvider` and `ILoggerFactory` natively.
    *   Implement output tagging and filtering to streamline intermediate agent outputs within multi-agent steps.
    *   Register both `OrchestratorHostBuilder` and `OrchestratorContextFactory` as Scoped in `ServiceCollectionExtensions.cs` to prevent captive dependency crashes.
    *   Populate `SpecialistBindings` in `OrchestratorContextFactory` instead of hardcoding `[]` to fix telemetry.

### Recommended Architectural Decision Records (ADRs)

To formalize these changes, four new ADRs should be drafted and stored in `docs/architecture/adr/`:

1.  **ADR-030: PGVector Database-Level Metadata Filtering**
    *   *Context:* Current in-memory filtering of semantic results introduces security and correctness risks.
    *   *Decision:* Mandate SQL-level metadata filtering for all vector store implementations.
2.  **ADR-031: Database-Backed Tenant Quota Enforcement**
    *   *Context:* In-memory quota tracking resets on app restarts and does not support multi-instance horizontal scaling. Direct database access on every request introduces performance bottlenecks.
    *   *Decision:* Migrate `QuotaEnforcer` to store quota limits and daily usages in PostgreSQL with sliding window logic, backed by a short-TTL memory caching layer (`IMemoryCache`).
3.  **ADR-032: REST API Contracts for Evaluation Suite Golden Sets**
    *   *Context:* Evaluation suite is functional but lacks API endpoints to manage datasets externally.
    *   *Decision:* Standardize JSON/REST payloads for Golden Set CRUD operations.
4.  **ADR-033: Upgrading Microsoft Agent Framework (MAF) to 1.9.0**
    *   *Context:* Version 1.6.2 of MAF contains experimental tags, AGUI bugs, and lacks dynamic output tagging. Incorrect DI lifecycles lead to captive dependency warnings/exceptions.
    *   *Decision:* Migrate the entire workspace to MAF 1.9.0, standardizing HarnessAgent DI registrations, stabilizing declarative workflows, populating specialist bindings in OrchestratorContextFactory, and registering both OrchestratorHostBuilder and OrchestratorContextFactory as Scoped.
