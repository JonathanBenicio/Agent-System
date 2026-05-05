@chat-dedicado @api-contract
Feature: Contrato da API para chat dedicado
  Como desenvolvedor
  Quero validar o contrato da API com targetAgent
  Para garantir compatibilidade entre frontend e backend

  # Nota: userId NÃO é enviado pelo cliente em nenhum canal.
  # - SignalR: ChatHub.SendMessage(string message, string? targetAgent) — 2 parâmetros.
  #   userId é extraído do ClaimsPrincipal autenticado.
  # - REST: ChatRequest aceita userId no body mas o handler IGNORA —
  #   usa httpContext.User (ClaimsPrincipal) para obter a identidade.

  Scenario: POST /api/chat com targetAgent presente
    Given o endpoint POST "/api/chat" está disponível
    And o usuário está autenticado via JWT ou ApiKey
    When envio um request com body:
      """
      {
        "message": "Analise este código",
        "targetAgent": "CodeReviewer"
      }
      """
    Then o response status é 200
    And o response body contém "agentName": "CodeReviewer"
    And o campo "success" é true
    And o response contém os campos: content, agentName, agentTier, actionsPerformed, toolsUsed, success, timestamp

  Scenario: POST /api/chat sem targetAgent mantém compatibilidade
    Given o endpoint POST "/api/chat" está disponível
    And o usuário está autenticado via JWT ou ApiKey
    When envio um request com body:
      """
      {
        "message": "Preciso de ajuda"
      }
      """
    Then o response status é 200
    And a requisição é processada via roteamento automático (ProcessRequestAsync)

  Scenario: POST /api/chat com targetAgent inexistente
    Given o endpoint POST "/api/chat" está disponível
    And o usuário está autenticado via JWT ou ApiKey
    When envio um request com body:
      """
      {
        "message": "Olá",
        "targetAgent": "AgentQueNaoExiste"
      }
      """
    Then o response status é 200
    And o campo "success" é false
    And o campo "errorMessage" contém "não encontrado"

  Scenario: SignalR SendMessage com targetAgent
    Given a conexão SignalR com "/hubs/chat" está estabelecida e autenticada
    When invoco "SendMessage" com args ["Analise X", "CodeReviewer"]
    Then o hub extrai userId do ClaimsPrincipal
    And processa via ProcessDirectRequestAsync
    And emite "ReceiveMessage" com campos: content, agentName, agentTier, actions, tools, success, sessionId, timestamp

  Scenario: SignalR SendMessage sem targetAgent (null)
    Given a conexão SignalR com "/hubs/chat" está estabelecida e autenticada
    When invoco "SendMessage" com args ["Analise X"]
    Then o hub extrai userId do ClaimsPrincipal
    And processa via ProcessRequestAsync (roteamento automático)
    And emite "ReceiveMessage" com a resposta do agent selecionado pelo MetaAgent
