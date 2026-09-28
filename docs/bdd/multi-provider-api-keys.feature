@seguranca @llm-providers @multi-key
Feature: Gerenciamento e Roteamento de Múltiplas API Keys por Provedor
  Como administrador do sistema
  Quero cadastrar múltiplas chaves de API para um mesmo provedor LLM
  Para permitir isolamento de modelos, controle de orçamentos e seleção explícita de credenciais no chat

  Background:
    Given o servidor AgenticSystem está em execução
    And o usuário está autenticado no tenant "tenant-financeiro"

  Scenario: Cadastrar uma nova API Key com sucesso e validar segurança visual
    When envio um POST para "/api/admin/llm/providers/Gemini/keys" com body:
      """
      {
        "name": "Gemini Desenvolvimento",
        "apiKey": "AIzaSyD-abc123secretkeyXYZ98765",
        "isDefault": true
      }
      """
    Then o response status é 200
    And o response contém os campos:
      | campo         | valor                   |
      | name          | Gemini Desenvolvimento  |
      | providerName  | Gemini                  |
      | lastFour      | 9876                    |
      | isDefault     | true                    |
      | isEnabled     | true                    |
    And o campo "encryptedValue" NÃO está presente no response
    And a chave secreta "AIzaSyD-abc123secretkeyXYZ98765" NÃO é exibida em logs ou respostas da API

  Scenario: Descoberta de modelos isolada por chave de API
    Given existe uma chave cadastrada com id "key-gemini-dev" para o provedor "Gemini" com a chave "AIzaSyD-validdevkey"
    When envio um POST para "/api/admin/llm/providers/Gemini/keys/key-gemini-dev/discover-models"
    Then o response status é 200
    And o campo "success" no response é true
    And o campo "discoveredModels" contém os modelos:
      | modelo            |
      | gemini-1.5-flash  |
      | gemini-1.5-pro    |
    And a chave "key-gemini-dev" possui apenas esses modelos associados no banco de dados

  Scenario: Roteamento de chamada de chat usando chave selecionada explicitamente
    Given existe uma chave cadastrada com id "key-gemini-prod" para o provedor "Gemini"
    And a chave "key-gemini-prod" está marcada como ativa e possui o modelo "gemini-1.5-pro"
    When o usuário inicia um chat selecionando o modelo "gemini-1.5-pro" e a credencial "key-gemini-prod"
    Then o backend deve invocar o provedor LLM "Gemini" utilizando a chave secreta descriptografada correspondente a "key-gemini-prod"
    And a resposta do chat é processada com sucesso

  Scenario: Fallback retrocompatível para chave global legada
    Given que nenhuma credencial está cadastrada na tabela "llm_provider_api_keys" para o provedor "Gemini"
    And existe uma chave global configurada na chave de configuração "llm.providers.gemini.apiKey"
    When o usuário inicia um chat utilizando o modelo padrão do Gemini
    Then o backend deve fazer o fallback automático e invocar a LLM utilizando a chave global legada
    And o chat funciona perfeitamente
