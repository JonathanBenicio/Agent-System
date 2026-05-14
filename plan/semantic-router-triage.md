  1 # Objective
    2 Implement a 3-tier Semantic Router / Triage Layer architecture to drastically reduce LLM costs and execution latency
      for low-complexity inputs, integrating seamlessly with the existing `FastPath` interceptors.
    3
    4 # Key Files & Context
    5 - `src/AgenticSystem.Core/Services/FastPath/ConversationalFastPathInterceptor.cs` (Camada 0: Heuristics)
    6 - `src/AgenticSystem.Core/Services/FastPath/MlFastPathInterceptor.cs` (Camada 0.5: Local ML.NET)
    7 - `src/AgenticSystem.Core/Interfaces/ISmartRouter.cs` (Camada 2 orchestrator)
    8 - **New File**: `src/AgenticSystem.Core/Services/Triage/ITriageService.cs`
    9 - **New File**: `src/AgenticSystem.Core/Services/Triage/TriageService.cs`
   10 - **New File**: `src/AgenticSystem.Core/Models/QueryTriageResult.cs`
   11
   12 # Implementation Steps
   13
   14 ## Step 1: Define the Triage Models (Camada 1)
   15 - Create `QueryTriageResult.cs` containing:
   16   - `IntentType` enum (`SmallTalk`, `DirectAnswer`, `ComplexReasoning`).
   17   - `ComplexityLevel` enum (`Low`, `Medium`, `High`).
   18   - Booleans for `RequiresRAG` and `RequiresTools`.
   19   - String for `RecommendedAgentTier`.
   20
   21 ## Step 2: Implement the Triage Service
   22 - Create `ITriageService` and its implementation `TriageService`.
   23 - Utilize `Microsoft.Extensions.AI` to prompt a cost-effective LLM (e.g., `gpt-4o-mini`).
   24 - Configure a strict system prompt instructing the model to act as a triage gatekeeper and return a typed JSON      
      response matching `QueryTriageResult`.
   25
   26 ## Step 3: Refactor SmartRouter for Multi-Tier Execution
   27 - Adapt `SmartRouter` (implementing `ISmartRouter`) to orchestrate the layered pipeline:
   28   1. **Camada 0 (Heuristic)**: Invoke `ConversationalFastPathInterceptor`. If triggered, return immediate response. 
   29   2. **Camada 0.5 (Local ML)**: Invoke `MlFastPathInterceptor`. If confidence is high, return offline fast-path     
      response.
   30   3. **Camada 1 (LLM Triage)**: Invoke `ITriageService.AnalyzeComplexityAsync()`.
   31   4. **Camada 2 (Tiered Execution)**: Switch based on `ComplexityLevel`:
   32      - `Low`: Execute Tier 1 logic (Direct prompt, mini model, minimal context).
   33      - `Medium`: Execute Tier 2 logic (RAG enabled, no sub-agents).
   34      - `High`: Execute Tier 3 logic (Full multi-agent orchestrator).
   35
   36 ## Step 4: Pipeline Integration
   37 - Integrate the newly structured `SmartRouter` into the primary request pipeline (e.g.,
      `AgentExecutionPreProcessingPipeline`) so that it serves as the ultimate ingress point before any heavy LLM loading 
      occurs.
   38
   39 # Verification & Testing
   40 - **Unit Tests**: Add tests verifying that `TriageService` correctly deserializes structured JSON responses into    
      `QueryTriageResult`.
   41 - **Integration Tests**: Verify the short-circuit behavior of `ConversationalFastPathInterceptor` and
      `MlFastPathInterceptor` within the new `SmartRouter` flow.
   42 - **Manual Verification**: Run queries of varying complexity to ensure logs reflect accurate tier selection (Low,   
      Medium, High) and significant latency drops for simple greetings.

