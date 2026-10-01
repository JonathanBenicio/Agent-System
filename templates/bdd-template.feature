Feature: [Nome da Funcionalidade. Ex: Execução de Tool de Pesquisa pelo Agente]
  Como um [ator/agente],
  Eu quero [capacidade/ferramenta],
  Para que [eu consiga resolver problemas complexos com dados atualizados].

  Background:
    Given que a configuração de gateway para provedores LLM está ativa
    And o plugin MCP "brave-search" está carregado na sessão

  Scenario: [Cenário de sucesso principal. Ex: Agente decide usar a pesquisa para uma pergunta factual]
    Given que a sessão "session-001" está ativa com o agente "research-agent"
    When o usuário envia o prompt "Quem venceu a copa do mundo de 2026?"
    Then o agente deve emitir um evento de "Execute Tool" para "brave_web_search"
    And a resposta final deve conter referências (citiations) aos resultados da pesquisa

  Scenario Outline: [Cenários baseados em variação de dados. Ex: Validação de permissões]
    Given que o usuário tem a role "<UserRole>"
    When ele tenta iniciar uma sessão com o agente "<AgentTier>"
    Then a API deve retornar o status code "<StatusCode>"

    Examples:
      | UserRole    | AgentTier   | StatusCode |
      | Admin       | Specialist  | 200        |
      | Standard    | Specialist  | 403        |
      | Standard    | Basic       | 200        |