# Plan: Configure CLI Extensions in Antigravity Kit

This plan outlines the architecture and changes required to integrate the newly installed Gemini CLI extensions into the **Antigravity Kit** local ecosystem (under `.agents/` and `GEMINI.md`).

## Project Type
- **Type**: WEB / GENERAL CORE ARCHITECTURE

---

## Success Criteria
1. **Conductor Thinking Integration**: The `/plan` workflow and `project-planner` agent are extended to apply Conductor's deep trade-off analysis, rigorous Socratic questioning, and structured documentation.
2. **Superpowers & Swarm Enforcement**: The global orchestrator (`orchestrator.md`) and global rules (`GEMINI.md`) are updated to ensure any parallel/concurrent subagents managed via `antigravity-swarm` strictly execute with the code quality and Clean Code guidelines of `obra/superpowers`.
3. **Security Gate Integration**: The `checklist.py` master validation script is updated to execute the `gemini-cli-extensions/security` scan as a hard barrier under the P0 block, preventing task completion or deployments if security flaws exist.

---

## Proposed File Changes

### 1. Global Rules & Master Orchestration
- [MODIFY] [GEMINI.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/GEMINI.md)
  - Unify the global Clean Code rule to refer to the **Superpowers Core Rules** for ultimate programming quality.
  - Define the integration of Conductor as the official plan thinking engine and Security as the final quality gate.

- [MODIFY] [.agents/agents/orchestrator.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/.agents/agents/orchestrator.md)
  - Configure the orchestrator as the Master Swarm Controller using `antigravity-swarm` protocols.
  - Inject Superpowers quality constraints into the parallel agent execution loop.
  - Require the Security Scan gate as the final sequential step of any execution.

- [MODIFY] [.agents/agents/project-planner.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/.agents/agents/project-planner.md)
  - Extend planning rules to incorporate the Conductor thinking framework.
  - Force exhaustive risk assessments, trade-offs comparison tables, and rollback plans for every plan generated.

### 2. Workflows Integration
- [MODIFY] [.agents/workflows/plan.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/.agents/workflows/plan.md)
  - Instruct the plan workflow to run the Conductor thinking pattern to draft the `docs/plan/{slug}.md`.

- [NEW] [.agents/workflows/security-gate.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/.agents/workflows/security-gate.md)
  - Create a dedicated workflow command to trigger security analysis and check the status of dependencies/secrets against the `gemini-cli-extensions/security` standard.

### 3. Master Scripts Integration
- [MODIFY] [.agents/scripts/checklist.py](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/.agents/scripts/checklist.py)
  - Update the "Security Scan" step to execute the `gemini-cli-extensions/security` validation alongside the Python static scanner.
  - Force a hard halt if any critical vulnerability is identified.

---

## Detailed Task Breakdown

### Phase 1: Planning & Conductor Thinking Integration
- **Task ID**: `plan_conductor`
- **Name**: Integrate Conductor into Planning Engine
- **Agent**: `project-planner`
- **Skills**: `agentic-brainstorming`, `plan-writing`
- **Priority**: P0
- **Dependencies**: None
- **INPUT**: `.agents/agents/project-planner.md` & `.agents/workflows/plan.md`
- **OUTPUT**: Updated planning prompt and workflow incorporating Conductor's structured thinking guidelines.
- **VERIFY**: Run `/plan` and ensure the generated plan conforms to the advanced Conductor structure (detailed trade-offs, architectural consequences, explicit rollbacks).

### Phase 2: Swarm & Superpowers Implementation
- **Task ID**: `swarm_superpowers`
- **Name**: Swarm Orchestration with Superpowers Quality
- **Agent**: `orchestrator`
- **Skills**: `parallel-agents`, `clean-code`
- **Priority**: P0
- **Dependencies**: `plan_conductor`
- **INPUT**: `GEMINI.md` & `.agents/agents/orchestrator.md`
- **OUTPUT**: Swarm coordination guidelines mapping subagent routing with Superpowers code-quality enforcement.
- **VERIFY**: Check `orchestrator.md` and `GEMINI.md` to ensure they strictly block subagents from writing code that doesn't pass Superpowers guidelines.

### Phase 3: Security Master Gate
- **Task ID**: `security_gate`
- **Name**: Integrate Security CLI into Checklist
- **Agent**: `security-auditor`
- **Skills**: `vulnerability-scanner`, `bash-linux`
- **Priority**: P0
- **Dependencies**: `swarm_superpowers`
- **INPUT**: `.agents/scripts/checklist.py`
- **OUTPUT**: Checklist calling the `gemini-cli-extensions/security` CLI engine during security validation.
- **VERIFY**: Execute `python .agents/scripts/checklist.py .` and ensure the security check launches.

---

## ✅ PHASE X: VERIFICATION PLAN
- [ ] Run checklist and ensure the new master checks execute correctly:
  ```bash
  python .agents/scripts/checklist.py .
  ```
- [ ] Check `/plan` execution to verify that Conductor thinking is active.
- [ ] Check `/security-gate` command response.
