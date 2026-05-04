@chat-dedicado @api-contract
Feature: Contrato da API para chat dedicado
  Como desenvolvedor
  Quero validar o contrato da API com targetAgent
  Para garantir compatibilidade entre frontend e backend

  Scenario: POST /api/chat com targetAgent presente
    Given o endpoint POST "/api/chat" está disponível
    When envio um request com body:
      """
      {
        "message": "Analise este código",
        "userId": "user-1",
        "targetAgent": "CodeReviewer"
      }
      """
    Then o response status é 200
    And o response body contém "agentName": "CodeReviewer"
    And o campo "isSuccess" é true

  Scenario: POST /api/chat sem targetAgent mantém compatibilidade
    Given o endpoint POST "/api/chat" está disponível
    When envio um request com body:
      """
      {
        "message": "Preciso de ajuda",
        "userId": "user-1"
      }
      """
    Then o response status é 200
    And a requisição é processada via roteamento automático

  Scenario: POST /api/chat com targetAgent inexistente
    Given o endpoint POST "/api/chat" está disponível
    When envio um request com body:
      """
      {
        "message": "Olá",
        "userId": "user-1",
        "targetAgent": "AgentQueNaoExiste"
      }
      """
    Then o response status é 200
    And o campo "isSuccess" é false
    And o campo "message" contém "não encontrado"

  Scenario: SignalR SendMessage com targetAgent
    Given a conexão SignalR com "/hubs/chat" está estabelecida
    When invoco "SendMessage" com args ["user-1", "Analise X", "CodeReviewer"]
    Then o hub processa via ProcessDirectRequestAsync
    And emite "ReceiveMessage" com a resposta do agent "CodeReviewer"

  Scenario: SignalR SendMessage sem targetAgent (null)
    Given a conexão SignalR com "/hubs/chat" está estabelecida
    When invoco "SendMessage" com args ["user-1", "Analise X", null]
    Then o hub processa via ProcessRequestAsync (roteamento automático)
    And emite "ReceiveMessage" com a resposta do agent selecionado pelo MetaAgent
