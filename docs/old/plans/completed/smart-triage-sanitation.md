# Implementation Plan: Smart Triage & Fast Path Alignment (Phase 6)

## Goal
Align the Smart Triage architecture (Regex/ML/LLM) across documentation, code, and infrastructure to ensure the Fast Path optimization is fully functional and documented.

## Tasks

### Phase 1: Architecture Documentation
- [x] **Task 1**: Create `docs/architecture/smart-routing-triage.md` explaining the 3-layer triage flow (Regex, ML, LLM). → Verify: File exists and is linked in `backend-architecture-explained.md`.

### Phase 2: Agent Factory & Specialization
- [x] **Task 2**: Add `DotNetExpertAgent` class to `AgenticSystem.Core.Agents.MasterAgents`. → Verify: Code compiles.
- [x] **Task 3**: Map `dotnet-expert` and `dotnet-self-learning-architect` in `HierarchicalAgentFactory.cs`. → Verify: Factory returns `DotNetExpertAgent` for these keys.

### Phase 3: Fast Path Infrastructure (ML.NET)
- [x] **Task 4**: Create a C# script/utility to generate a basic `fastpath_model.zip` with sample intents (Greeting, SmallTalk). → Verify: `fastpath_model.zip` exists in root.
- [x] **Task 5**: Update `ServiceCollectionExtensions.cs` to ensure `PredictionEnginePool` is correctly initialized when the model exists. → Verify: No startup errors.

### Phase 4: Final Verification
- [x] **Task 6**: Run `checklist.py` to ensure all architectural rules are respected. → Verify: Script returns success.

## Done When
- [x] `fastpath_model.zip` is present and `MlFastPathInterceptor` is active.
- [x] `TriageService` suggestions for `.NET` agents are correctly resolved to `DotNetExpertAgent`.
- [x] The Triage architecture is fully documented.

## Notes
- The ML model will be a simple "Multiclass Classification" model trained on a small dataset of greetings and small talk.
- This phase resolves the "Silent Gaps" found during the inspection of `MlFastPathInterceptor`.
