Feature: Integração Unificada do Chat (12 Pilares)
  Como um usuário final corporativo (tenant),
  Eu quero que o Chat Principal integre de forma unificada e multi-tenant todas as ferramentas, RAG, Memória, Workflows e Skills,
  Para que eu consiga interagir de forma rica, ativa e segura com os meus dados empresariais.

  Background:
    Given que o hub SignalR "/hubs/chat" está ativo e escutando conexões
    And o banco de dados Postgres com suporte pgvector está online
    And o catálogo de ferramentas possui a "TenantAnalyticsTool" registrada no "IToolManager"

  Scenario Outline: Isolamento Estrito de RAG por Salas de Conhecimento (Zero Trust)
    Given que a sessão está ativa sob o tenant "<TenantId>"
    And o agente especialista está associado às salas de conhecimento "<SalasAssociadas>"
    When o usuário faz a pergunta "Quais são as metas do cronograma?"
    Then a busca vetorial no RAG deve filtrar os chunks pela cláusula "room_id IN (<SalasAssociadas>) AND tenant_id = <TenantId>"
    And o resultado de RAG retornado deve ser "<StatusRAG>"

    Examples:
      | TenantId   | SalasAssociadas  | StatusRAG |
      | tenant-123 | room-A, room-B   | populado  |
      | tenant-123 |                  | vazio     |
      | tenant-999 | room-C           | populado  |

  Scenario: Execução Segura de Queries Analíticas SQL via Tool Parametrizada
    Given que a sessão está ativa sob o tenant "tenant-123"
    And o agente possui a ferramenta "TenantAnalyticsTool" ativa no catálogo
    When o usuário solicita "mostre a receita total deste trimestre por mês"
    Then o agente deve disparar uma chamada de tool para "TenantAnalyticsTool" com a ação "GetRevenueSummary"
    And a execução no banco Postgres deve aplicar estritamente a parametrização sob o escopo "tenant_id = 'tenant-123'"
    And a resposta analítica deve ser exibida formatada no chat

  Scenario: Fallback Resiliente de Ferramentas MCP Externas
    Given que a sessão está ativa com o servidor MCP externo "StitchMCP"
    And o agente possui a ferramenta remota "github-list-issues" mapeada via gateway
    When o servidor MCP "StitchMCP" falha por timeout de 3000ms
    Then o "IToolManager" deve disparar o Circuit Breaker da ferramenta remota
    And o agente deve tratar o erro graciosamente notificando "Capacidade temporariamente indisponível"
    And a sessão de chat do usuário deve permanecer ativa e funcional
