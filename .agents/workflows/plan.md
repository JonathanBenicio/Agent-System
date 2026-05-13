---
description: Create project plan using project-planner agent. No code writing - only plan file generation.
---

# /plan - Project Planning Mode

$ARGUMENTS

---

## 🔴 CRITICAL RULES

1. **NO CODE WRITING** - This command creates plan file only
2. **Use project-planner agent** - NOT Antigravity Agent's native Plan mode
3. **Socratic Gate** - Ask clarifying questions before planning
4. **Dynamic Naming** - Plan file named based on task

---

## Task

Use the `project-planner` agent with this context:

```
CONTEXT:
- User Request: $ARGUMENTS
- Mode: PLANNING ONLY (no code)
- Output: docs/plan/{task-slug}.md (dynamic naming)

NAMING RULES:
1. Extract 2-3 key words from request
2. Lowercase, hyphen-separated
3. Max 30 characters
4. Example: "e-commerce cart" → docs/plan/ecommerce-cart.md

RULES:
1. Follow project-planner.md Phase -1 (Context Check)
2. Follow project-planner.md Phase 0 (Socratic Gate)
3. Create {slug}.md under docs/plan/ with task breakdown
4. Enforce Conductor Blueprint rules (Trade-offs comparing at least 2 paths, Risk Matrix with mitigations, detailed Rollback strategies, and absolute/relative file pathing details).
5. DO NOT write any code files
6. REPORT the exact file name created
```

---

## Expected Output

| Deliverable | Location |
|-------------|----------|
| Project Plan | `docs/plan/{task-slug}.md` |
| Conductor Blueprint (Trade-offs, Risks, Rollback, Paths) | Inside plan file |
| Task Breakdown | Inside plan file |
| Agent Assignments | Inside plan file |
| Verification Checklist | Phase X in plan file |

---

## After Planning

Tell user:
```
[OK] Plan created: docs/plan/{slug}.md

Next steps:
- Review the plan
- Run `/create` to start implementation
- Or modify plan manually
```

---

## Naming Examples

| Request | Plan File |
|---------|-----------|
| `/plan e-commerce site with cart` | `docs/plan/ecommerce-cart.md` |
| `/plan mobile app for fitness` | `docs/plan/fitness-app.md` |
| `/plan add dark mode feature` | `docs/plan/dark-mode.md` |
| `/plan fix authentication bug` | `docs/plan/auth-fix.md` |
| `/plan SaaS dashboard` | `docs/plan/saas-dashboard.md` |

---

## Usage

```
/plan e-commerce site with cart
/plan mobile app for fitness tracking
/plan SaaS dashboard with analytics
```
