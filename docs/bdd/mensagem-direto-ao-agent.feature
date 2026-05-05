@chat-dedicado @backend
Feature: Mensagem vai direto ao agent selecionado
  Como usuário no chat dedicado
  Quero que minhas mensagens sejam processadas diretamente pelo agent alvo
  Para obter respostas sem análise de contexto intermediária

  Background:
    Given o sistema possui agents registrados e ativos
    And o usuário está autenticado
    And existe um agent "SecurityAnalyst" ativo

  Scenario: Mensagem via SignalR é enviada com targetAgent
    Given o usuário está na página "/chat/SecurityAnalyst"
    And a conexão SignalR está estabelecida e autenticada
    When o usuário envia a mensagem "Analise vulnerabilidades no endpoint /api/users"
    Then o hub SignalR recebe "SendMessage" com mensagem e targetAgent "SecurityAnalyst"
    And o userId é extraído do ClaimsPrincipal (não enviado pelo cliente)
    And o MetaAgentOrchestrator chama ProcessDirectRequestAsync com targetAgent "SecurityAnalyst"
    And a resposta é do agent "SecurityAnalyst" sem passar pela análise de contexto

  Scenario: Mensagem via REST API é enviada com targetAgent
    Given o usuário está na página "/chat/SecurityAnalyst"
    And a conexão SignalR não está disponível
    When o usuário envia a mensagem "Verifique o OWASP Top 10"
    Then o POST para "/api/chat" inclui "targetAgent": "SecurityAnalyst" no body
    And a resposta é processada diretamente pelo agent "SecurityAnalyst"

  Scenario: ProcessDirectRequestAsync bypassa análise de contexto
    Given o backend recebe um request com targetAgent "SecurityAnalyst"
    When o MetaAgentOrchestrator processa a requisição
    Then não executa a análise de contexto (ContextAnalysis)
    And cria um AnalysisResult direcionado ao agent "SecurityAnalyst"
    And delega a execução diretamente ao agent encontrado
    And registra o evento na sessão com directRequest = true
