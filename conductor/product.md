# Initial Concept
Sistema Agentic Generalista — A framework-first hosted orchestration system using .NET 10, Microsoft Agent Framework, and Microsoft.Extensions.AI. It features Obsidian memory, PostgreSQL with pgvector, and supports A2A, AG-UI, MCP, and OpenAI-compatible surfaces.

## Target Audience
Developers, system administrators, and AI integrators needing a centralized, governable, and extensible platform for executing agentic workflows.

## Key Features
- Centralized agent execution via `AgentExecutionWorkflow`.
- End-to-end streaming through SignalR and Server-Sent Events (SSE).
- Dedicated Chat for direct agent interaction bypassing automatic routing.
- Active Voice Assistant prototype via `/api/voice`.
- Authenticated MCP server for agent listing, RAG querying, and execution.
- Tool governance with risk policies, approval workflows, and auditing.
- Human-in-the-loop approvals for sensitive actions.

## Success Metrics
- Seamless execution of multi-agent workflows.
- Extensibility via MCP plugins and experimental tracks without destabilizing the core.
- Transparent and auditable tool execution.