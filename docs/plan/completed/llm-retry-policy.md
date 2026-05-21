# Retry Policy for LLMManager

> **Status:** ✅ CONCLUÍDO — Política de resiliência implementada e compilando
> **Verificado:** 2026-05-20

## Goal
Implement a resilience policy in `LLMManager.cs` that combines a Circuit Breaker with a Wait and Retry strategy (Exponential Backoff + Jitter) to handle 429 errors from LLM providers.

## Tasks
- [x] **Task 1: Update private fields** → Replace `_circuitBreakers` dictionary with `_resiliencePolicies` that stores a tuple of `(AsyncPolicyWrap Policy, AsyncCircuitBreakerPolicy CircuitBreaker)`.
- [x] **Task 2: Implement `GetResiliencePolicy`** → Replace `GetCircuitBreaker` with a method that builds and returns both Circuit Breaker and Retry policies wrapped together.
    - CB: Same as existing (429, timeout, HttpRequestException, TimeoutException; 3 errors; 30s break).
    - Retry: 4 attempts, Exponential Backoff ($2^{attempt}$ seconds), plus 0-1000ms jitter.
- [x] **Task 3: Refactor `GenerateAsync`** → Call `GetResiliencePolicy`, check Circuit State from the tuple's CircuitBreaker, and execute via the PolicyWrap.
- [x] **Task 4: Verification** → Run `dotnet build src/AgenticSystem.Infrastructure` to ensure compilation.

## Done When
- [x] `LLMManager` uses a wrapped policy (CB + Retry).
- [x] Retry logic uses exponential backoff with jitter.
- [x] Code compiles without errors.
