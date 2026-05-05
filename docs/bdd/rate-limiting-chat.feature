@seguranca @rate-limiting
Feature: Rate Limiting no endpoint de chat
  Como sistema
  Quero limitar requisições por tenant ao POST /api/chat
  Para proteger contra abuso e garantir disponibilidade

  # Implementação: sliding window por tenant_id, 60s de janela.
  # Limite padrão: TenantContext.Limits.MaxRequestsPerMinute ?? 30.

  Background:
    Given o servidor AgenticSystem está em execução
    And o usuário está autenticado

  Scenario: Requisições dentro do limite são aceitas
    Given o tenant "tenant-1" tem limite de 30 requisições por minuto
    And o tenant "tenant-1" enviou 5 requisições nos últimos 60 segundos
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 200

  Scenario: Requisição que excede o limite retorna 429
    Given o tenant "tenant-1" tem limite de 30 requisições por minuto
    And o tenant "tenant-1" enviou 30 requisições nos últimos 60 segundos
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 429
    And o response body contém "Rate limit exceeded. Try again later."

  Scenario: Janela deslizante libera após expiração
    Given o tenant "tenant-1" atingiu o limite de 30 requisições
    And 61 segundos se passaram desde a primeira requisição da janela
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 200
    And as requisições expiradas foram removidas da janela

  Scenario: Rate limiting é isolado por tenant
    Given o tenant "tenant-1" atingiu o limite de 30 requisições
    And o tenant "tenant-2" enviou 0 requisições
    When o tenant "tenant-2" envia POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 200
    And o tenant "tenant-1" continua bloqueado

  Scenario: Tenant sem configuração usa limite padrão de 30
    Given o tenant "tenant-novo" não tem configuração de limites (Limits é null)
    When o tenant "tenant-novo" envia 31 requisições em sequência
    Then as primeiras 30 retornam status 200
    And a 31ª retorna status 429

  Scenario: Validação de mensagem obrigatória
    Given o usuário está autenticado
    When envio POST "/api/chat" com body:
      """
      { "message": "" }
      """
    Then o response status é 400
    And o response body contém "Message is required."

  Scenario: Validação de tamanho máximo da mensagem
    Given o usuário está autenticado
    When envio POST "/api/chat" com mensagem de 10001 caracteres
    Then o response status é 400
    And o response body contém "Message exceeds maximum length of 10000 characters."
