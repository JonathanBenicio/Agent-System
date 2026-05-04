# AgenticSystem — Frontend

SPA React + TypeScript para o sistema agentic de IA. Interface completa com dashboard, gestão de agents, chat em tempo real via SignalR, gateway monitoring e dark mode.

## Stack

| Camada | Tecnologia | Versão |
|--------|-----------|--------|
| Framework | React | 19.x |
| Build | Vite | 8.x |
| Linguagem | TypeScript | 6.x |
| Estilo | TailwindCSS v4 | 4.x |
| UI Pattern | shadcn/ui (CVA + clsx + twMerge) | — |
| Ícones | Lucide React | 1.x |
| Real-time | @microsoft/signalr | 10.x |
| Roteamento | react-router-dom | 7.x |
| Markdown | react-markdown | 10.x |

## Requisitos

- Node.js 20+
- Backend rodando em `https://localhost:5001` (SignalR + REST API)

## Setup

```bash
npm install
npm run dev
```

O dev server sobe em `http://localhost:5173` com proxy automático:

| Rota | Destino |
|------|---------|
| `/api/*` | `https://localhost:5001` |
| `/hubs/*` | `https://localhost:5001` (WebSocket) |

## Scripts

| Comando | Descrição |
|---------|-----------|
| `npm run dev` | Dev server com HMR |
| `npm run build` | Build de produção (tsc + vite) |
| `npm run preview` | Preview do build local |
| `npm run lint` | ESLint |

## Estrutura

```
src/
├── main.tsx                           # Entry point
├── App.tsx                            # Router + layout
├── index.css                          # TailwindCSS v4 + custom styles
├── assets/                            # Imagens e SVGs
│   ├── hero.png
│   ├── react.svg
│   └── vite.svg
├── components/
│   ├── layout/
│   │   ├── Layout.tsx                 # Shell: sidebar + outlet + status bar
│   │   ├── Sidebar.tsx                # Navegação colapsável
│   │   └── StatusBar.tsx              # Status de conexão + versão
│   ├── chat/
│   │   ├── ChatPage.tsx               # Chat genérico (roteamento automático)
│   │   ├── AgentChatPage.tsx          # Chat dedicado (direto ao agent)
│   │   ├── MessageList.tsx            # Lista de mensagens com auto-scroll
│   │   ├── MessageBubble.tsx          # Bubble com markdown, badges, tools
│   │   ├── ChatInput.tsx              # Textarea auto-resize + envio
│   │   └── ProcessingIndicator.tsx    # Dots animados
│   ├── agents/
│   │   ├── AgentsPage.tsx             # Lista/CRUD de agents com "Chat direto"
│   │   ├── AgentDetailModal.tsx       # Detalhes do agent
│   │   ├── AgentFormModal.tsx         # Form de criação/edição
│   │   ├── SkillsPage.tsx             # Gestão de skills
│   │   └── ToolsPage.tsx              # Gestão de tools
│   ├── dashboard/
│   │   └── DashboardPage.tsx          # Dashboard geral do sistema
│   ├── gateway/
│   │   ├── ServicesPage.tsx            # Status dos serviços externos
│   │   ├── CostsPage.tsx              # Custos por provider/agent
│   │   └── HealthPage.tsx             # Health checks
│   ├── llm/
│   │   └── ProvidersPage.tsx          # Gestão de LLM providers
│   ├── plugins/
│   │   ├── PluginsPage.tsx            # Lista de plugins MCP
│   │   ├── PluginDetailModal.tsx      # Detalhes do plugin
│   │   └── PluginLoadModal.tsx        # Carregamento de plugin
│   ├── rag/
│   │   └── RAGPage.tsx                # Pipeline RAG & documentos
│   ├── settings/
│   │   └── SettingsPage.tsx           # Configurações do sistema
│   ├── shared/
│   │   ├── Badge.tsx                  # Badge reutilizável
│   │   ├── ConfirmModal.tsx           # Modal de confirmação
│   │   ├── Loading.tsx                # Spinner/loading
│   │   └── Toast.tsx                  # Notificações toast
│   └── PlaceholderPage.tsx            # Tela "em desenvolvimento"
├── hooks/
│   ├── useAgents.ts                   # CRUD + listagem de agents
│   ├── useChat.ts                     # SignalR + REST fallback + state (aceita targetAgent)
│   ├── useDashboard.ts                # Métricas do dashboard
│   ├── useGatewayServices.ts          # Status do gateway
│   ├── useLLMProviders.ts             # Gestão de LLM providers
│   ├── usePlugins.ts                  # Gestão de plugins MCP
│   ├── useSettings.ts                 # Configurações
│   ├── useSkills.ts                   # Listagem de skills
│   └── useTools.ts                    # Listagem de tools
├── lib/
│   ├── api.ts                         # Cliente HTTP (fetch wrapper)
│   ├── signalr.ts                     # Singleton de conexão SignalR (chat)
│   ├── signalr-gateway.ts             # Conexão SignalR (gateway events)
│   └── utils.ts                       # cn() — clsx + twMerge
└── types/
    ├── api.ts                         # Interfaces: Agent, Tool, Skill, Provider, etc.
    └── chat.ts                        # Interfaces: ChatMessage, ChatSession, etc.
```

## Rotas

| Path | Componente | Descrição | Status |
|------|-----------|-----------|--------|
| `/` | `ChatPage` | Chat com roteamento automático (MetaAgent) | ✅ Implementado |
| `/chat/:agentName` | `AgentChatPage` | Chat dedicado direto ao agent | ✅ Implementado |
| `/dashboard` | `DashboardPage` | Dashboard geral do sistema | ✅ Implementado |
| `/agents` | `AgentsPage` | Lista/CRUD de agents + botão "Chat direto" | ✅ Implementado |
| `/tools` | `ToolsPage` | Gestão de tools | ✅ Implementado |
| `/skills` | `SkillsPage` | Gestão de skills | ✅ Implementado |
| `/rag` | `RAGPage` | Pipeline RAG & documentos | ✅ Implementado |
| `/gateway` | `ServicesPage` | Status dos serviços do gateway | ✅ Implementado |
| `/gateway/health` | `HealthPage` | Health checks dos providers | ✅ Implementado |
| `/costs` | `CostsPage` | Custos por provider/agent/sessão | ✅ Implementado |
| `/providers` | `ProvidersPage` | Gestão de LLM providers | ✅ Implementado |
| `/plugins` | `PluginsPage` | Plugins MCP | ✅ Implementado |
| `/config` | `SettingsPage` | Configurações do sistema | ✅ Implementado |

## Comunicação com Backend

### SignalR — Chat (`/hubs/chat`)

| Evento | Direção | Descrição |
|--------|---------|-----------|
| `SendMessage` | Client → Server | Envia mensagem (userId, message, targetAgent?) |
| `ReceiveMessage` | Server → Client | Resposta do agent com metadata |
| `ProcessingStarted` | Server → Client | Indica início de processamento |
| `ReceiveError` | Server → Client | Erro no processamento |
| `Connected` | Server → Client | Confirmação de conexão |

### SignalR — Gateway (`/hubs/gateway`)

| Evento | Direção | Descrição |
|--------|---------|-----------|
| `ServiceStatusChanged` | Server → Client | Mudança de status de provider |
| `CostAlertTriggered` | Server → Client | Alerta de budget |
| `CircuitStateChanged` | Server → Client | Mudança de estado do circuit breaker |
| `RateLimitWarning` | Server → Client | Warning de rate limit |

### REST API

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/api/chat` | Envio de mensagem (com targetAgent opcional) |
| `GET` | `/api/agent/agents` | Lista agents |
| `GET` | `/api/agent/agents/{id}` | Detalhes de um agent |
| `POST` | `/api/agent/agents` | Criar agent dinâmico |
| `DELETE` | `/api/agent/agents/{id}` | Remover agent |
| `GET` | `/api/admin/gateway/dashboard` | Snapshot do gateway |
| `GET` | `/api/admin/gateway/health` | Health checks |
| `GET` | `/api/admin/gateway/costs` | Custos |
| `GET` | `/api/admin/llm/providers` | LLM providers |
| `GET` | `/api/admin/mcp/plugins` | Plugins MCP |
| `GET` | `/api/admin/settings` | Configurações |
| `GET` | `/health` | Health check geral |

## Convenções

- **Path alias**: `@/` resolve para `./src`
- **Dark mode**: Classe `dark` no `<html>`, background `zinc-950`
- **Componentes**: Functional components com TypeScript
- **Estilo**: Utility-first com Tailwind, `cn()` para merge condicional
- **Hooks**: Um hook por domínio funcional (agents, chat, gateway, etc.)
- **SignalR**: Duas conexões — chat (interativo) e gateway (monitoring)
