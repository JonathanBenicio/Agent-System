# BDD — Cenários de Teste (Chat Dedicado)

Cenários Gherkin para validação funcional das user stories US-31, US-32 e US-33 (Chat Dedicado).

## Features

| Arquivo | Descrição | US |
|---------|-----------|-----|
| `chat-dedicado-via-lista.feature` | Botão "Chat direto" na lista de agents, navegação para `/chat/{agentName}` | US-31 |
| `mensagem-direto-ao-agent.feature` | Mensagem processada diretamente pelo agent alvo sem roteamento automático | US-32 |
| `contrato-api-chat-dedicado.feature` | Contrato da API — parâmetro `targetAgent` em SignalR e REST | US-32 |
| `historico-separado-por-agent.feature` | Histórico do chat dedicado é isolado do chat genérico | US-33 |
| `voltar-roteamento-automatico.feature` | Ao navegar para `/`, volta ao roteamento automático (MetaAgent) | US-33 |
| `agent-nao-encontrado.feature` | Erro quando `targetAgent` não existe no registry | US-32 |

## Formato

Os cenários seguem o padrão **Gherkin** (Given/When/Then):

```gherkin
Feature: Chat dedicado via lista de agents

  Scenario: Usuário abre chat direto a partir da lista
    Given estou na página "/agents"
    And vejo o agent "CodeReview" na lista
    When clico no botão "Chat direto" do agent "CodeReview"
    Then sou redirecionado para "/chat/CodeReview"
    And vejo o header com "Chat — CodeReview"
```

## Como Executar

### Validação Manual

Os cenários servem como **especificação executável** — podem ser validados manualmente seguindo os steps:

1. Abra o frontend (`npm run dev`)
2. Para cada cenário, execute os **Given** (pré-condições), **When** (ações) e **Then** (validações)
3. Marque cada critério de aceite na US correspondente

### Automação com Cypress

Para automação futura, os cenários podem ser convertidos em testes Cypress:

```bash
# Estrutura sugerida
frontend/cypress/e2e/
├── chat-dedicado/
│   ├── via-lista.cy.ts
│   ├── mensagem-direta.cy.ts
│   ├── historico-separado.cy.ts
│   └── agent-nao-encontrado.cy.ts
```

### Automação com Playwright

Alternativa com Playwright (se adotado):

```bash
frontend/e2e/
├── chat-dedicado.spec.ts
```

## Cobertura

| US | Cenários | Happy Path | Edge Cases |
|----|:--------:|:----------:|:----------:|
| US-31 | 5 | 3 | 2 |
| US-32 | 7 | 4 | 3 |
| US-33 | 5 | 3 | 2 |
| **Total** | **17** | **10** | **7** |

## Referências

- [USER-STORIES.md](../USER-STORIES.md) — User stories completas com critérios de aceite
- [INDEX.md](../INDEX.md) — Índice geral da documentação
