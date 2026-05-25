# AgenticSystem — Frontend

O **AgenticSystem Frontend** é uma Single Page Application (SPA) construída com as tecnologias mais recentes do ecossistema React. Ele fornece a interface de usuário para interação com o orquestrador de agentes, monitoramento do sistema via Gateway, gestão de recursos avançados (Skills, Tools e Agents) e comunicação em tempo real via SignalR.

## 🚀 Tecnologias

- **Framework**: React 19.x
- **Build Tool**: Vite 8.x
- **Linguagem**: TypeScript 6.x
- **Estilização**: TailwindCSS v4
- **Componentes**: shadcn/ui (CVA + clsx + twMerge)
- **Ícones**: Lucide React 1.x
- **Tempo Real**: @microsoft/signalr 10.x
- **Roteamento**: react-router-dom 7.x
- **Markdown**: react-markdown 10.x

## 📋 Pré-requisitos

- Node.js 20+
- Backend (`AgenticSystem.Api`) rodando localmente (normalmente em `https://localhost:5001`).

## 🛠️ Instalação e Execução

Para iniciar o desenvolvimento local:

```bash
# Instalar dependências
npm install

# Rodar o servidor de desenvolvimento
npm run dev
```

O dev server subirá em `http://localhost:5173`. Ele está configurado com um proxy automático para facilitar a integração com o backend:
- Requisições para `/api/*` são roteadas para `https://localhost:5001`
- Conexões WebSocket para `/hubs/*` são roteadas para `https://localhost:5001`

### Scripts Úteis

- `npm run build`: Executa a validação de tipos (tsc) e gera o build otimizado de produção via Vite.
- `npm run lint`: Executa a validação de código usando ESLint.
- `npm run preview`: Sobe um servidor local para testar a versão gerada no build.
- `npm run cy:run`: Executa testes end-to-end (E2E) via Cypress.

## 📂 Estrutura do Projeto

A arquitetura de pastas está organizada da seguinte forma:

```
src/
├── main.tsx                           # Entry point
├── App.tsx                            # Router + layout
├── index.css                          # TailwindCSS v4 + custom styles
├── assets/                            # Imagens e SVGs
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
│   │   ├── ServicesPage.tsx           # Status dos serviços externos
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

## 🗺️ Rotas e Telas (Screens)

O sistema possui roteamento dinâmico mapeando as seguintes rotas e interfaces:

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

### Descrição Detalhada das Telas

- **ChatPage (`/`)**: Tela principal de interação conversacional baseada em chat. O usuário conversa com o `MetaAgent` (agente chefe) que faz o roteamento inteligente das mensagens em tempo real para os agentes especialistas conforme a intenção. Suporta visualização de logs operacionais de execução passo-a-passo.
- **AgentChatPage (`/chat/:agentName`)**: Interface de chat dedicada para interação direta e contínua com um único agente especialista selecionado (ex: `PersonalAgent`, `LearningAgent`). Pula a etapa de classificação do `MetaAgent`.
- **DashboardPage (`/dashboard`)**: Painel administrativo principal. Traz gráficos e estatísticas gerais de uso do ecossistema, incluindo quantidade de sessões abertas, erros ocorridos, volumetria de mensagens e status de resiliência.
- **AgentsPage (`/agents`)**: Interface de Gerenciamento de Agentes. Exibe o cadastro de agentes ativos, permitindo a criação dinâmica de novos agentes com instruções de sistema (System Prompts) personalizadas, além de sua edição ou exclusão.
- **ToolsPage (`/tools`)**: Tela para inventário e auditoria das ferramentas (Tools) habilitadas no backend que podem ser invocadas pelos agentes. Mostra detalhes de permissão e descrição de inputs e parâmetros.
- **SkillsPage (`/skills`)**: Tela de gerenciamento do acervo de Habilidades (Skills) do sistema. Skills são diretrizes de comportamento estáticas inseridas dinamicamente no contexto do agente.
- **RAGPage (`/rag`)**: Portal de controle do RAG (Geração Aumentada de Recuperação). Permite que o usuário faça ingestão de novos documentos textuais ou Markdown, consulte as estatísticas do pipeline e faça buscas vetoriais testes diretamente nas bases indexadas.
- **ServicesPage (`/gateway`)**: Tela do painel de controle do *Gateway de Serviços*. Oferece visão em tempo real sobre a taxa de requisições enviadas para as LLMs externas, o estado ativo do *Circuit Breaker* de cada provedor e latências operacionais.
- **HealthPage (`/gateway/health`)**: Dashboard técnico com dados de testes de saúde (*health checks*) contínuos dos microsserviços do backend, garantindo detecção instantânea de instabilidade de API ou banco de dados.
- **CostsPage (`/costs`)**: Central de custos e *FinOps*. Exibe gráficos de gastos em USD por agente, por sessão de chat e por provedor LLM. Notifica de alertas de estouro do orçamento (*daily budget*) definido.
- **ProvidersPage (`/providers`)**: Painel de gerenciamento das credenciais das IAs. O usuário pode alternar provedores (ex: desabilitar temporariamente OpenAI e forçar Ollama local) e ajustar a prioridade de fallback em tempo de execução.
- **PluginsPage (`/plugins`)**: Interface de plugins compatíveis com o Model Context Protocol (MCP). Permite registrar e monitorar servidores MCP de terceiros acoplados à aplicação.
- **SettingsPage (`/config`)**: Painel que reúne as configurações de ambiente em execução (runtime parameters), configurações do Service Gateway e parâmetros do Hybrid Chunking no RAG.

## 📡 Integração com o Backend

A interface se comunica com o backend primariamente de duas formas:

1. **SignalR (Tempo Real)**:
   - `/hubs/chat`: Responsável pelo streaming de respostas dos agentes, envio de mensagens e notificações de estado (processando, erro, etc.).
   - `/hubs/gateway`: Fornece métricas de monitoramento em tempo real (ex: status de serviços, alertas de custos, acionamento do circuit breaker).

2. **REST API**:
   - Fornece endpoints CRUD para gerenciamento de configurações, provedores LLM, agentes dinâmicos, métricas históricas, entre outros.

## 🎨 Padrões de Desenvolvimento

- Sempre utilize a função utilitária `cn()` localizada em `src/lib/utils.ts` para mesclar classes condicionalmente, especialmente útil ao trabalhar com as variáveis do Tailwind.
- Siga as regras do **Clean Code**. Separe o que é lógica de estado dos componentes visuais através de Custom Hooks.
- Evite placeholders; construa uma UI sempre que possível.
