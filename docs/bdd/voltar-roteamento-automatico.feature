@chat-dedicado @routing
Feature: Voltar para roteamento automático
  Como usuário
  Quero poder alternar entre chat dedicado e chat genérico
  Para escolher quando usar o roteamento automático do MetaAgent

  Background:
    Given o sistema possui agents registrados e ativos
    And o usuário está autenticado

  Scenario: Chat genérico usa roteamento automático (sem targetAgent)
    Given o usuário está na página "/"
    When o usuário envia a mensagem "Preciso de ajuda com testes"
    Then o hub SignalR recebe "SendMessage" com targetAgent null
    And o MetaAgentOrchestrator executa ProcessRequestAsync (roteamento automático)
    And o MetaAgent analisa o contexto e seleciona o melhor agent

  Scenario: Navegar de chat dedicado para chat genérico
    Given o usuário está na página "/chat/CodeReviewer"
    When o usuário navega para "/"
    Then o chat genérico é exibido sem targetAgent
    And o placeholder exibe "Envie uma mensagem..."
    And as mensagens anteriores do chat dedicado não aparecem

  Scenario: Footer do chat genérico indica roteamento automático
    Given o usuário está na página "/"
    Then o footer do input exibe "O AgenticSystem seleciona automaticamente o melhor agent para cada solicitação."
