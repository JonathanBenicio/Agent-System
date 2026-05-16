# language: pt
Funcionalidade: Workflow Orchestration
  Como administrador do AgenticSystem
  Quero gerenciar e executar workflows visuais
  Para orquestrar tarefas entre agentes e ferramentas

  Contexto:
    Dado que estou autenticado como administrador

  # ──────────────────────────────────────────────
  # US-34 — Visualizar e manipular Canvas de Workflows
  # ──────────────────────────────────────────────

  Cenário: Visualizar canvas de workflows
    Dado que acesso a página "/workflows"
    Então um canvas interativo é exibido
    E vejo a toolbar com ações de gerenciamento
    E o painel de status do motor exibe os nós e conexões ativos

  Cenário: Adicionar nó de agente no canvas
    Dado que estou na página "/workflows"
    Quando clico no botão "Add Agent"
    Então um novo nó do tipo "Agent" é adicionado ao canvas com dados padrão
    E o contador de nós ativos no painel é incrementado

  Cenário: Adicionar nó de ferramenta no canvas
    Dado que estou na página "/workflows"
    Quando clico no botão "Add Tool"
    Então um novo nó do tipo "Tool" é adicionado ao canvas com dados padrão
    E o contador de nós ativos no painel é incrementado

  # ──────────────────────────────────────────────
  # US-35 — Salvar e Executar Workflow
  # ──────────────────────────────────────────────

  Cenário: Salvar definição do workflow (Estado Alvo)
    Dado que construí um workflow no canvas com nós e conexões
    Quando clico no botão "Save Workflow"
    Então o sistema gera a definição JSON do workflow
    E envia para a API via POST /api/workflows
    E exibe um feedback visual de sucesso

  Cenário: Executar workflow a partir do builder
    Dado que estou na página "/workflows" com um workflow válido
    Quando clico no botão "Run"
    Então o sistema dispara a execução do workflow no backend via POST /api/workflows/run
    E exibe o status de execução no painel
