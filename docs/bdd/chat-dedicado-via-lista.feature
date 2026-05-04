@chat-dedicado
Feature: Chat dedicado via lista de agents
  Como usuário do sistema
  Quero abrir um chat direto com um agent específico
  Para enviar mensagens sem passar pelo roteamento automático do MetaAgent

  Background:
    Given o sistema possui agents registrados e ativos
    And o usuário está autenticado

  Scenario: Abrir chat dedicado via botão na lista de agents
    Given o usuário está na página "/agents"
    And existe um agent "CodeReviewer" ativo
    When o usuário clica no botão "Chat direto" do agent "CodeReviewer"
    Then o sistema navega para "/chat/CodeReviewer"
    And o header exibe "Chat com CodeReviewer"
    And o placeholder do input exibe "Envie uma mensagem para CodeReviewer..."

  Scenario: Chat dedicado exibe informações do agent no header
    Given o usuário está na página "/chat/CodeReviewer"
    Then o header exibe o ícone do agent
    And o header exibe "Chat com CodeReviewer"
    And o subtítulo indica "Mensagens vão direto para este agent"

  Scenario: Botão de voltar retorna à lista de agents
    Given o usuário está na página "/chat/CodeReviewer"
    When o usuário clica no botão de voltar
    Then o sistema navega para "/agents"
