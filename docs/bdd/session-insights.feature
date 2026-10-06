Funcionalidade: Insights de Sessão & Histórico de Execução
  Como um usuário avançado
  Quero visualizar insights gerados por IA e o histórico detalhado de workflows
  Para entender o comportamento dos agentes e auditar processos automatizados

  Contexto:
    Dado que estou autenticado no sistema

  # ──────────────────────────────────────────────
  # SESSION INSIGHTS (CHAT)
  # ──────────────────────────────────────────────

  Cenário: Visualizar insights automáticos de uma conversa
    Dado que terminei uma conversa com o agente "Orchestrator"
    Quando o SessionConsolidator processa a sessão "sess-123"
    E eu abro a aba de "Insights" na interface de chat
    Então o sistema chama GET /api/session/sess-123
    E o campo "insights" no JSON de resposta deve conter:
      | Campo                   | Descrição                                      |
      | facts                   | Informações novas extraídas do diálogo         |
      | decisions               | Escolhas feitas durante a interação            |
      | preferences             | Gostos ou restrições detectadas                |
      | actionItems             | Itens de ação (action items) sugeridos         |

  Cenário: Exibir estado vazio quando não há insights
    Dado que uma nova sessão de chat foi iniciada e não tem conteúdo relevante
    Quando abro a aba de "Insights"
    Então deve ser exibida a mensagem "Nenhum insight extraído desta sessão ainda."

  # ──────────────────────────────────────────────
  # WORKFLOW EXECUTION HISTORY
  # ──────────────────────────────────────────────

  Cenário: Listar histórico de execuções de um workflow
    Dado que estou na página de "Workflow Builder" (/workflows)
    E o workflow "Data Migration" está selecionado
    Quando clico no ícone de "Relógio" (History)
    Então o painel lateral "Execution History" é exibido
    E o sistema chama GET /api/workflows/{id}/executions
    E exibe uma lista de cards com Status (Completed/Failed/Running) e Timestamp

  Cenário: Detalhar uma execução específica de workflow
    Dado que o painel de histórico está aberto
    Quando clico em uma execução com status "Failed"
    Então o sistema chama GET /api/workflows/execution/{exec-id}
    E exibe a linha do tempo (timeline) de cada step executado
    E o step que falhou deve mostrar a mensagem de erro técnica

  Cenário: Visualizar outputs de um step do workflow
    Dado que estou visualizando os detalhes da execução "exec-888"
    Quando o step "Format JSON" foi concluído com sucesso
    Então devo conseguir expandir a seção "Output" do step
    E visualizar o JSON resultante formatado em um bloco de código (pre/code)

  Cenário: Atualização em tempo real de execução em curso
    Dado que disparei a execução de um workflow agora
    Quando a execução está com status "Running"
    Então o ícone de status deve exibir uma animação de pulso (pulse)
    E ao finalizar, o status deve mudar automaticamente para "Completed" ou "Failed" sem recarregar a página
