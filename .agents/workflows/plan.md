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
4. **Use Template** - Use the template in `templates/plan-template.md` as the base for the plan file.
5. **Dynamic Naming** - Plan file named based on task

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
3. Create {slug}.md under docs/plan/ using templates/plan-template.md
4. Fill in all sections of the template with task-specific details.
5. DO NOT write any code files
6. REPORT the exact file name created
```

---

## Expected Output

| Deliverable | Location |
|-------------|----------|
| Project Plan | `docs/plan/{task-slug}.md` |

---

## After Planning

Tell user:
```
[OK] Plan created: docs/plan/{slug}.md using templates/plan-template.md

Next steps:
- Review the plan
- Run `/create` to start implementation
- Or modify plan manually
```

---

## Usage
```
/plan e-commerce site with cart
/plan mobile app for fitness tracking
```
