# Implementation Plan - Agent Factory & Dynamic Agent Builder

This plan outlines the transition of the `AgenticSystem` from static agent definitions to a dynamic, no-code platform where agents are assembled from capabilities, tools, and policies based on user intent.

## Goal Description
Transform the current agent creation logic into a robust "Agent Factory" paradigm. This allows users to describe an objective (e.g., "Analyze a football match") and have the system generate an executable "Agent Manifest" composed of specialized capabilities, validated tools, and safety policies.

## User Review Required

> [!IMPORTANT]
> **Architectural Shift**: We are moving agent definitions from code/static config to a dynamic metadata layer. This requires database schema updates.
> 
> **Autonomy & Safety**: High-autonomy agents (L4/L5) will default to "Human-in-the-loop" approval gates for any tool labeled with `Medium` or higher risk.

## Proposed Changes

### 1. Core Models & Schema Expansion
Expand the foundational models to support the "Agent Manifest" concept.

#### [MODIFY] [AgentModels.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Core/Models/AgentModels.cs)
- **AgentSpecification**: Add `Capabilities` (List<string>), `AutonomyLevel` (Enum), and `PolicyIds` (List<string>).
- **AgentInfo**: Include the full manifest details for runtime inspection.

#### [NEW] [CapabilityModels.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Core/Models/CapabilityModels.cs)
- Define `Capability`: A high-level abstraction (e.g., `license_plate_recognition`) that maps to a set of required tools and recommended models.

---

### 2. The Registry Layer
Create the internal "Catalog" of what the platform can actually do.

#### [NEW] [CapabilityRegistry.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Core/Services/CapabilityRegistry.cs)
- Hard-coded (initially) registry of capabilities (Vision Pack, Sports Pack, Finance Pack).
- Provides tool-resolution logic for the Orchestrator.

#### [MODIFY] [DynamicAgentService.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Core/Services/DynamicAgentService.cs)
- **Prompt Engineering**: Update the system prompt to use the `CapabilityRegistry` as context for the LLM.
- **Parsing**: Handle the mapping from natural language intent to specific capabilities and autonomy levels.

---

### 3. Governance & Policy Integration
Automate the attachment of guardrails during agent creation.

#### [MODIFY] [PolicyEngine.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Core/Services/PolicyEngine.cs)
- Expose methods to generate "Default Guardrail Policies" based on the agent domain (e.g., Privacy policies for Camera agents).

#### [MODIFY] [HierarchicalAgentFactory.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Core/Services/HierarchicalAgentFactory.cs)
- Update instantiation logic to register the agent with the `PolicyEngine` immediately upon creation.

---

### 4. Specialized Tooling ("The Packs")
Implement foundational tools for the requested use cases.

#### [NEW] [VisionPackTools.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Core/Tools/VisionPackTools.cs)
- `Vision.PlateDetect`: Placeholder/Integration point for OCR/YOLO.
- `Vision.VehicleDetect`: Placeholder for object detection.

---

## Verification Plan

### Automated Tests
- `DynamicAgentServiceTests`: Verify that "monitor my camera" results in an `AgentSpecification` with `AutonomyLevel.L3` and `Vision` capabilities.
- `CapabilityRegistryTests`: Ensure capabilities resolve to the correct tool dependencies.

### Manual Verification
- **Scenario**: Request "Crie um agente para vigiar meu portão e ler placas".
- **Validation**: Check if the agent manifest includes `vision.plate_detect` and a policy requiring approval for external notifications.
