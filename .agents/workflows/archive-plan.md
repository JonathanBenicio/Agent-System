---
description: Move completed plan files to docs/plan/completed/ directory.
---

# /archive-plan - Archive Completed Plans

$ARGUMENTS

---

## 🔴 CRITICAL RULES

1. **Verify Completion**: Only archive plans that are marked as `COMPLETED` or have all tasks checked `[x]`.
2. **Path Correction**: Move from `docs/plan/` to `docs/plan/completed/`.
3. **No Content Loss**: Do not modify the content of the plan, only move the file.

---

## Task

Use the `orchestrator` or `project-planner` agent with this context:

```
CONTEXT:
- User Request: $ARGUMENTS (Name of the plan file to archive)
- Mode: FILE MANAGEMENT
- Action: Move file from docs/plan/ to docs/plan/completed/

STEPS:
1. Identify the plan file specified in $ARGUMENTS.
2. Open and read the file to verify status (look for `status: COMPLETED` or all tasks marked as `[x]`).
3. If not completed, ask the user if they want to archive it anyway.
4. Ensure the directory `docs/plan/completed/` exists.
5. Move the file to `docs/plan/completed/`.
6. Report success to the user.
```

---

## Expected Output

| Deliverable | Location |
|-------------|----------|
| Archived Plan | `docs/plan/completed/{plan-name}.md` |

---

## Usage

```
/archive-plan smart-triage-sanitation.md
/archive-plan p3-extensibility-automation.md
```
