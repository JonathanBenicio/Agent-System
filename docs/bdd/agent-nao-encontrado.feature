@chat-dedicado @error-handling
Feature: Agent não encontrado no chat dedicado
  Como sistema
  Quero informar o usuário quando o agent alvo não existe
  Para que ele possa corrigir a navegação ou usar o roteamento automático

  Background:
    Given o sistema possui agents registrados e ativos
    And o usuário está autenticado

  Scenario: Enviar mensagem para agent inexistente via SignalR
    Given o usuário está na página "/chat/AgentInexistente"
    And a conexão SignalR está estabelecida
    When o usuário envia a mensagem "Olá"
    Then o MetaAgentOrchestrator retorna erro "Agent 'AgentInexistente' não encontrado."
    And a mensagem de erro é exibida no chat com source "MetaAgent"

  Scenario: Enviar mensagem para agent inexistente via REST
    Given o usuário está na página "/chat/AgentInexistente"
    And a conexão SignalR não está disponível
    When o usuário envia a mensagem "Olá"
    Then o POST para "/api/chat" com targetAgent "AgentInexistente" retorna erro
    And a resposta contém "Agent 'AgentInexistente' não encontrado."

  Scenario: URL com nome de agent inválido exibe chat vazio
    Given o usuário navega para "/chat/!@#$%"
    Then a página de chat dedicado é renderizada
    And ao enviar uma mensagem, retorna erro de agent não encontrado
