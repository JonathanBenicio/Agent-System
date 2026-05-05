@seguranca @autenticacao
Feature: Autenticação Multi-Scheme (JWT + ApiKey)
  Como sistema
  Quero suportar autenticação via JWT (Authorization header) ou ApiKey (X-Api-Key header)
  Para permitir acesso tanto por aplicações frontend (JWT) quanto por integrações (ApiKey)

  # PolicyScheme "MultiAuth": se Authorization header presente → JWT; senão → ApiKey.

  Background:
    Given o servidor AgenticSystem está em execução

  # ── JWT ────────────────────────────────────────────────

  Scenario: Requisição com JWT válido é autenticada
    Given o header "Authorization" contém "Bearer <jwt_valido>"
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 200
    And o userId é extraído das claims do JWT (NameIdentifier ou sub)

  Scenario: Requisição com JWT expirado é rejeitada
    Given o header "Authorization" contém "Bearer <jwt_expirado>"
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 401

  Scenario: Requisição com JWT assinado com chave errada é rejeitada
    Given o header "Authorization" contém "Bearer <jwt_chave_invalida>"
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 401

  # ── ApiKey ─────────────────────────────────────────────

  Scenario: Requisição com X-Api-Key válido é autenticada
    Given o header "X-Api-Key" contém a chave configurada em AgenticSystem:AdminApiKey
    And nenhum header "Authorization" está presente
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 200
    And o userId é "admin" com role "Admin"

  Scenario: Requisição com X-Api-Key inválido é rejeitada
    Given o header "X-Api-Key" contém "chave-errada"
    And nenhum header "Authorization" está presente
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 401

  Scenario: Requisição sem nenhum header de autenticação é rejeitada
    Given nenhum header "Authorization" está presente
    And nenhum header "X-Api-Key" está presente
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o response status é 401

  # ── Prioridade ─────────────────────────────────────────

  Scenario: Authorization header tem prioridade sobre X-Api-Key
    Given o header "Authorization" contém "Bearer <jwt_valido>"
    And o header "X-Api-Key" contém a chave válida
    When envio POST "/api/chat" com body:
      """
      { "message": "Olá" }
      """
    Then o esquema JWT é utilizado (ForwardDefaultSelector seleciona JWT quando Authorization está presente)
    And o response status é 200

  # ── SignalR ────────────────────────────────────────────

  Scenario: Hub SignalR requer autenticação
    Given nenhuma credencial foi fornecida
    When tento conectar ao hub "/hubs/chat"
    Then a conexão é rejeitada com erro de autenticação

  Scenario: Hub SignalR aceita JWT via query string (transport WebSocket)
    Given o token JWT válido é enviado via query string "access_token"
    When conecto ao hub "/hubs/chat"
    Then a conexão é estabelecida com sucesso
    And o userId é extraído das claims do token
