# Product Guidelines

## Prose Style
- **Tone:** Professional, clear, and direct. The focus is on technical accuracy and ease of use.
- **Language:** Brazilian Portuguese (as inferred from the docs) mixed with standard English for technical terms.
- **Terminology:** Consistent use of terms like "Agents", "Workflows", "Tools", "MCP", and "RAG".

## Branding & Visual Identity
- **UI Framework:** Tailwind CSS with a clean, modern aesthetic (dark-mode preferred).
- **Components:** Atomic design, prioritizing reusable and accessible components.
- **Typography:** Legible sans-serif fonts optimized for technical dashboards and chat interfaces.

## UX Principles
- **Clarity over Complexity:** Experimental features should not clutter the core user path.
- **Observability:** Actions taken by agents must be transparent, traceable, and auditable.
- **Human-in-the-Loop:** Sensitive actions require explicit approval before execution.
- **Responsiveness:** Real-time feedback via SignalR/SSE is critical for a good agentic chat experience.