# Project Guidelines

## Product Boundary

- Preserve the stable product core as the default journey: main chat, session lifecycle, streaming, one primary execution path, and minimum observability.
- Prefer extending the current runtime path instead of adding a second parallel architecture, a duplicate chat path, or a competing orchestration entry point.
- Use [docs/architecture/backend-architecture-explained.md](../docs/architecture/backend-architecture-explained.md) as the source of truth for the current runtime topology and subsystem details.

## Stable Core And Lab Zone

- Stable core: main chat, session handling, streaming, one execution path, and minimum observability required to operate the product.
- Lab zone: extra protocols, MCP plugins, collaborative workflows, advanced approvals, self-improvement loops, and specialized administrative surfaces.
- Keep lab capabilities isolated from the stable core by default. They should not become part of the main product path unless the task explicitly promotes them.
- When a task touches lab capabilities, keep them behind feature flags, in separate modules, and with optional rollout.

## Experimental Capability Rules

- Every experimental capability must start with a clear hypothesis, a success criterion, and a removal criterion.
- Design experiments so they can be enabled, measured, rolled back, or deleted without reworking the main chat and session flow.
- Add only the observability needed to compare flagged behavior against the baseline before expanding scope.
- Keep future-capability planning aligned with [docs/planejamento/AI_Advanced_Capabilities_Roadmap.md](../docs/planejamento/AI_Advanced_Capabilities_Roadmap.md) and subsystem details in [docs/architecture/backend-architecture-explained.md](../docs/architecture/backend-architecture-explained.md).