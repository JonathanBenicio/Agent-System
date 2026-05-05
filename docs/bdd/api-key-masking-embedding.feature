@seguranca @api-key-masking
Feature: Mascaramento de API Keys no Embedding Migration
  Como sistema
  Quero que API keys nunca sejam expostas em respostas da API
  Para evitar vazamento de credenciais sensíveis

  # Implementação: EmbeddingMigrationController.MaskApiKey() retorna "********"
  # para qualquer ApiKey não-nula, e null para ApiKey vazia/nula.

  Background:
    Given o servidor AgenticSystem está em execução
    And o usuário está autenticado
    And existem modelos de embedding configurados

  Scenario: GET /api/admin/embedding-migration/models retorna API keys mascaradas
    Given existe um modelo "openai-ada" com ApiKey "sk-abc123secretkey"
    When envio GET "/api/admin/embedding-migration/models"
    Then o response status é 200
    And o campo "apiKey" do modelo "openai-ada" é "********"
    And o campo "apiKey" NÃO contém "sk-abc123secretkey"

  Scenario: GET /api/admin/embedding-migration/models/{modelId} mascara a key
    Given existe um modelo "openai-ada" com ApiKey "sk-abc123secretkey"
    When envio GET "/api/admin/embedding-migration/models/openai-ada"
    Then o response status é 200
    And o campo "apiKey" é "********"

  Scenario: GET /api/admin/embedding-migration/models/active mascara a key
    Given o modelo ativo "openai-ada" tem ApiKey "sk-abc123secretkey"
    When envio GET "/api/admin/embedding-migration/models/active"
    Then o response status é 200
    And o campo "apiKey" é "********"

  Scenario: POST /api/admin/embedding-migration/models retorna key mascarada
    When envio POST "/api/admin/embedding-migration/models" com body:
      """
      {
        "id": "new-model",
        "name": "New Model",
        "provider": "OpenAI",
        "modelName": "text-embedding-3-small",
        "dimensions": 1536,
        "apiKey": "sk-newsecretkey123"
      }
      """
    Then o response status é 200
    And o campo "apiKey" no response é "********"
    And o campo "apiKey" NÃO contém "sk-newsecretkey123"

  Scenario: Modelo sem API key retorna null (não "********")
    Given existe um modelo "local-model" com ApiKey vazia ou nula
    When envio GET "/api/admin/embedding-migration/models/local-model"
    Then o response status é 200
    And o campo "apiKey" é null

  Scenario: Response preserva todos os outros campos do modelo
    Given existe um modelo com:
      | campo      | valor                       |
      | id         | openai-ada                  |
      | name       | OpenAI Ada                  |
      | provider   | OpenAI                      |
      | modelName  | text-embedding-ada-002      |
      | dimensions | 1536                        |
      | baseUrl    | https://api.openai.com      |
      | isActive   | true                        |
    When envio GET "/api/admin/embedding-migration/models/openai-ada"
    Then o response contém:
      | campo      | valor                       |
      | id         | openai-ada                  |
      | name       | OpenAI Ada                  |
      | provider   | OpenAI                      |
      | modelName  | text-embedding-ada-002      |
      | dimensions | 1536                        |
      | baseUrl    | https://api.openai.com      |
      | isActive   | true                        |
      | apiKey     | ********                    |
