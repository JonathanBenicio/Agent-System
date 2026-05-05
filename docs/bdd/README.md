# BDD — Cenários de Teste

Cenários Gherkin para validação funcional do AgenticSystem.
Todos os arquivos `.feature` usam `# language: pt` (Gherkin em português).

---

## Features — Chat Interface (US-01 a US-04)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `chat-interface.feature` | Envio de mensagem, histórico, target agent, chat API | US-01 a US-04 | 14 |

## Features — Chat Dedicado (US-31 a US-33)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `chat-dedicado-via-lista.feature` | Botão "Chat direto" na lista de agents | US-31 | 3 |
| `mensagem-direto-ao-agent.feature` | Mensagem diretamente ao agent alvo | US-32 | ~3 |
| `contrato-api-chat-dedicado.feature` | Contrato da API — `targetAgent` em SignalR e REST | US-32 | ~3 |
| `agent-nao-encontrado.feature` | Erro quando `targetAgent` não existe | US-32 | ~2 |
| `historico-separado-por-agent.feature` | Histórico isolado do chat genérico | US-33 | ~2 |
| `voltar-roteamento-automatico.feature` | Retorno ao roteamento automático | US-33 | ~2 |

## Features — Gateway Dashboard (US-05 a US-08)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `gateway-dashboard.feature` | Métricas, serviços, saúde e custos do gateway | US-05 a US-08 | 14 |

## Features — Agent Management (US-09 a US-14)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `agent-management.feature` | CRUD de agents, filtro por tier, tools, skills, cleanup | US-09 a US-14 | 20 |

## Features — LLM Providers (US-15, US-16)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `llm-providers.feature` | Gerenciamento de providers, teste de conexão, mascaramento de API Key | US-15, US-16 | 9 |

## Features — Settings e Configuration (US-17 a US-19 + ML22)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `settings-config.feature` | Gateway settings, memory settings, tabs, Config CRUD, audit log, secrets | US-17 a US-19 | 15 |

## Features — MCP Plugins (US-20 a US-22)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `mcp-plugins.feature` | Listar, carregar, remover plugins; tools e resources via MCP | US-20 a US-22 | 12 |

## Features — SignalR Real-time (US-23 a US-25)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `signalr-realtime.feature` | ChatHub, GatewayHub, streaming, reconexão, indicador de processamento | US-23 a US-25 | 11 |

## Features — Transversal e UX (US-26 a US-30)

| Arquivo | Descrição | US | Cenários |
|---------|-----------|-----|:--------:|
| `transversal-ux.feature` | Sidebar, toast, confirm modal, loading/empty/error states, dark theme | US-26 a US-30 | 16 |

## Features — Segurança e Infraestrutura

| Arquivo | Descrição | Área | Cenários |
|---------|-----------|------|:--------:|
| `autenticacao-multi-scheme.feature` | JWT + ApiKey via PolicyScheme "MultiAuth" | Segurança | 9 |
| `rate-limiting-chat.feature` | Sliding window rate limiter por tenant | Segurança | 7 |
| `api-key-masking-embedding.feature` | Mascaramento de API keys (Embedding Migration) | Segurança | 6 |

## Features — Backend APIs

| Arquivo | Descrição | Área | Cenários |
|---------|-----------|------|:--------:|
| `backend-apis.feature` | Documents/RAG, Planner, Voice, Obsidian, Setup, Health, Version | T2, ML3, ML18, T8, ML15 | 19 |
| `scheduled-tasks.feature` | Tasks CRUD, Rules/Trigger Engine, Channels, Health | ML21 | 18 |
| `embedding-migration.feature` | Models CRUD, Migration Jobs lifecycle, Switchover | ML23 | 14 |

---

## Resumo de Cobertura

| Grupo | Feature Files | Cenários | User Stories |
|-------|:------------:|:--------:|:------------:|
| Chat Interface | 1 | 14 | US-01 a US-04 |
| Chat Dedicado | 6 | ~15 | US-31 a US-33 |
| Gateway Dashboard | 1 | 14 | US-05 a US-08 |
| Agent Management | 1 | 20 | US-09 a US-14 |
| LLM Providers | 1 | 9 | US-15, US-16 |
| Settings/Config | 1 | 15 | US-17 a US-19, ML22 |
| MCP Plugins | 1 | 12 | US-20 a US-22 |
| SignalR Real-time | 1 | 11 | US-23 a US-25 |
| Transversal/UX | 1 | 16 | US-26 a US-30 |
| Segurança | 3 | 22 | T4, T5 |
| Backend APIs | 3 | 51 | T2, ML3, ML18, T8, ML15, ML21, ML23 |
| **Total** | **20** | **~199** | **33 US + 12 MLs + 5 Ts** |

## Formato

Todos os cenários seguem o padrão **Gherkin** em português:

```gherkin
# language: pt
Funcionalidade: Nome da feature
  Como [persona]
  Quero [ação]
  Para [benefício]

  Cenário: Descrição do cenário
    Dado [pré-condição]
    Quando [ação]
    Então [resultado esperado]
```

## Como Executar

### Validação Manual

Os cenários servem como **especificação executável**:

1. Abra o frontend (`npm run dev`) e/ou backend (`dotnet run`)
2. Para cada cenário, execute os **Dado** (pré-condições), **Quando** (ações) e **Então** (validações)
3. Marque cada critério de aceite na US correspondente

### Automação com Cypress

```bash
frontend/cypress/e2e/
├── chat/
├── gateway/
├── agents/
├── providers/
├── plugins/
├── settings/
└── security/
```

### Automação com Playwright

```bash
frontend/e2e/
├── chat.spec.ts
├── gateway.spec.ts
├── agents.spec.ts
└── ...
```

## Referências

- [USER-STORIES.md](../USER-STORIES.md) — User stories completas com critérios de aceite
- [INDEX.md](../INDEX.md) — Índice geral da documentação
