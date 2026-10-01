# Phase 2 DI Saneamento and Microsoft.Agents 1.9.0 Upgrade Plan

## Goal
Implement DI Validation, convert Orchestrator factory/host builder to Scoped, eliminate double-build and sync-over-async in Orchestrator execution, and upgrade all Microsoft.Agents packages to version 1.9.0.

## Tasks
- [x] Task 1: Enable DI Validation (scopes and on-build validation) in `Program.cs` -> Verify: Inspect lines 18-22 in `Program.cs`.
- [x] Task 2: Create `OrchestratorContextState.cs` in Infrastructure project -> Verify: File exists at `src/AgenticSystem.Infrastructure/AgentFramework/OrchestratorContextState.cs`.
- [x] Task 3: Refactor registrations to Scoped and add OrchestratorContextState registration in `ServiceCollectionExtensions.cs` -> Verify: Check registrations for `OrchestratorHostBuilder`, `OrchestratorContextFactory`, `RAGContextProvider`, `OrchestratorContextState`, and `OrchestratorContext` in `ServiceCollectionExtensions.cs`.
- [x] Task 4: Modify `BuildHandoffWorkflowAsync` in `OrchestratorHostBuilder.cs` to accept the pre-built `AIAgent` directly -> Verify: Signature and implementation changed, eliminating `BuildAsync` invocation inside it.
- [x] Task 5: Refactor `ExecuteAsync` in `FrameworkOrchestratorService.cs` to fetch active agents asynchronously, build the agent, populate `OrchestratorContextState`, resolve scoped services cleanly, pass pre-built agent, and implement case-insensitive matching for specialist telemetry -> Verify: `ExecuteAsync` implementation avoids double build and sync-over-async, and maps `calledAgentName` to the correct `IAgent` object.
- [x] Task 6: Upgrade Microsoft.Agents packages to 1.9.0 in the 4 `.csproj` files, remove warning disables -> Verify: Packages upgraded to 1.9.0, `<NoWarn>$(NoWarn);MAAIW001</NoWarn>` removed, `#pragma warning` disables removed.
- [x] Task 7: Build the project and run all tests -> Verify: Project compiles cleanly and `dotnet test` returns success for 687 passed tests and 1 PostgreSQL integration test skipped.

## Done When
- [x] Project compiles with no warnings/errors on DI scopes and packages.
- [x] All runnable backend tests pass; the PostgreSQL integration test remains skipped.
