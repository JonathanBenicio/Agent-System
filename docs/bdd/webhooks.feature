# language: pt
Funcionalidade: Webhooks Integration
  Como administrador do AgenticSystem
  Quero gerenciar webhooks de entrada e saída
  Para integrar o sistema com serviços externos

  Contexto:
    Dado que estou autenticado como administrador

  # ──────────────────────────────────────────────
  # US-36 — Gerenciar Webhooks (CRUD)
  # ──────────────────────────────────────────────

  Cenário: Listar webhooks cadastrados
    Dado que acesso a página "/webhooks"
    Quando a lista é carregada via GET /api/gateway/webhooks
    Então vejo a tabela de webhooks com URL, evento e status
    E o contador de total de webhooks exibe o número correto

  Cenário: Cadastrar novo webhook
    Dado que estou na página "/webhooks"
    Quando preencho o formulário de criação com nome "Slack Hook", URL "https://hooks.slack.com/...", evento "agent_triggered"
    E clico em "Registrar Webhook"
    Então o sistema chama POST /api/gateway/webhooks com os dados do formulário
    E um toast de sucesso é exibido
    E o novo webhook aparece na lista

  Cenário: Excluir webhook cadastrado
    Dado que o webhook "Slack Hook" existe na lista
    Quando clico no botão de lixeira no item "Slack Hook"
    Então o sistema chama DELETE /api/gateway/webhooks/{id}
    E o item é removido da interface
    E um toast de sucesso é exibido

  # ──────────────────────────────────────────────
  # US-37 — Simular e Testar Recebimento
  # ──────────────────────────────────────────────

  Cenário: Simular envio de evento para webhook (Teste)
    Dado que estou na página "/webhooks"
    Quando clico no botão de teste em um webhook cadastrado
    Então o sistema envia um payload de teste via POST para a URL do webhook
    E exibe o status da resposta (ex: 200 OK) na interface
