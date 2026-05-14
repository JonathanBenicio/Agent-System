# Retry Policy for LLMManager

## Goal
Implement a resilience policy in `LLMManager.cs` that combines a Circuit Breaker with a Wait and Retry strategy (Exponential Backoff + Jitter) to handle 429 errors from LLM providers.

## Tasks
- [ ] **Task 1: Update private fields** → Replace `_circuitBreakers` dictionary with `_resiliencePolicies` that stores a tuple of `(AsyncPolicyWrap Policy, AsyncCircuitBreakerPolicy CircuitBreaker)`.
- [ ] **Task 2: Implement `GetResiliencePolicy`** → Replace `GetCircuitBreaker` with a method that builds and returns both Circuit Breaker and Retry policies wrapped together.
    - CB: Same as existing (429, timeout, HttpRequestException, TimeoutException; 3 errors; 30s break).
    - Retry: 4 attempts, Exponential Backoff ($2^{attempt}$ seconds), plus 0-1000ms jitter.
- [ ] **Task 3: Refactor `GenerateAsync`** → Call `GetResiliencePolicy`, check Circuit State from the tuple's CircuitBreaker, and execute via the PolicyWrap.
- [ ] **Task 4: Verification** → Run `dotnet build src/AgenticSystem.Infrastructure` to ensure compilation.

## Done When
- [ ] `LLMManager` uses a wrapped policy (CB + Retry).
- [ ] Retry logic uses exponential backoff with jitter.
- [ ] Code compiles without errors.
