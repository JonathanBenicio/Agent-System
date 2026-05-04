@chat-dedicado @sessions
Feature: Histórico separado por agent
  Como usuário
  Quero que o histórico de chat dedicado seja independente do chat genérico
  Para manter contexto separado por agent

  Background:
    Given o sistema possui agents registrados e ativos
    And o usuário está autenticado

  Scenario: Mensagens no chat dedicado não aparecem no chat genérico
    Given o usuário está na página "/chat/SecurityAnalyst"
    And o usuário enviou "Analise vulnerabilidades"
    And o agent respondeu "Iniciando análise de segurança..."
    When o usuário navega para "/"
    Then o chat genérico não exibe as mensagens do chat dedicado

  Scenario: Cada chat dedicado tem seu próprio histórico
    Given o usuário trocou mensagens no "/chat/SecurityAnalyst"
    When o usuário navega para "/chat/CodeReviewer"
    Then o chat com CodeReviewer não exibe mensagens do SecurityAnalyst
    And o histórico começa vazio

  Scenario: Sessão do backend registra directRequest
    Given o usuário está no chat dedicado com "SecurityAnalyst"
    When envia uma mensagem
    Then o SessionManager registra o evento com metadata directRequest = true
    And o sessionId é único por contexto de chat dedicado
