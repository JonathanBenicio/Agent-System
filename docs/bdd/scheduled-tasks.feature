# language: pt
Funcionalidade: Scheduled Tasks e Trigger Engine
  Como administrador do AgenticSystem
  Quero gerenciar tarefas agendadas e regras de trigger
  Para automatizar operações recorrentes e reativos a eventos

  Contexto:
    Dado que estou autenticado como administrador

  # ══════════════════════════════════════════════
  # TASKS CRUD (ML21)
  # ══════════════════════════════════════════════

  Cenário: Listar tarefas agendadas
    Quando faço GET /api/admin/scheduled-tasks/tasks
    Então o status de resposta é 200
    E a lista de tarefas agendadas é retornada

  Cenário: Consultar tarefa por ID
    Dado que a tarefa "task-001" existe
    Quando faço GET /api/admin/scheduled-tasks/tasks/task-001
    Então o status de resposta é 200
    E os detalhes da tarefa são retornados (nome, cron, status, última execução)

  Cenário: Criar nova tarefa agendada
    Quando faço POST /api/admin/scheduled-tasks/tasks com body:
      """json
      {
        "name": "cleanup-sessions",
        "cronExpression": "0 2 * * *",
        "action": "CleanupInactiveSessions",
        "enabled": true
      }
      """
    Então o status de resposta é 200
    E a tarefa é criada e agendada

  Cenário: Pausar tarefa ativa
    Dado que a tarefa "task-001" está em execução
    Quando faço POST /api/admin/scheduled-tasks/tasks/task-001/pause
    Então o status de resposta é 200
    E a tarefa é pausada

  Cenário: Resumir tarefa pausada
    Dado que a tarefa "task-001" está pausada
    Quando faço POST /api/admin/scheduled-tasks/tasks/task-001/resume
    Então o status de resposta é 200
    E a tarefa volta a executar no próximo cron

  Cenário: Executar tarefa manualmente
    Dado que a tarefa "task-001" existe
    Quando faço POST /api/admin/scheduled-tasks/tasks/task-001/execute
    Então o status de resposta é 200
    E a tarefa é executada imediatamente (fora do agendamento)

  Cenário: Deletar tarefa
    Dado que a tarefa "task-001" existe
    Quando faço DELETE /api/admin/scheduled-tasks/tasks/task-001
    Então o status de resposta é 200
    E a tarefa é removida do scheduler

  # ══════════════════════════════════════════════
  # RULES CRUD (Trigger Engine)
  # ══════════════════════════════════════════════

  Cenário: Listar regras de trigger
    Quando faço GET /api/admin/scheduled-tasks/rules
    Então o status de resposta é 200
    E a lista de regras é retornada

  Cenário: Consultar regra por ID
    Dado que a regra "rule-001" existe
    Quando faço GET /api/admin/scheduled-tasks/rules/rule-001
    Então o status de resposta é 200
    E os detalhes da regra são retornados (condição, ação, status)

  Cenário: Criar nova regra de trigger
    Quando faço POST /api/admin/scheduled-tasks/rules com body:
      """json
      {
        "name": "high-error-rate",
        "condition": "error_rate > 5%",
        "action": "NotifySlack",
        "channel": "slack",
        "enabled": true
      }
      """
    Então o status de resposta é 200
    E a regra é criada e ativada

  Cenário: Atualizar regra existente
    Dado que a regra "rule-001" existe
    Quando faço PUT /api/admin/scheduled-tasks/rules/rule-001 com novo threshold
    Então o status de resposta é 200
    E a regra é atualizada

  Cenário: Habilitar e desabilitar regra
    Dado que a regra "rule-001" está desabilitada
    Quando faço POST /api/admin/scheduled-tasks/rules/rule-001/enable
    Então o status de resposta é 200
    E a regra passa a avaliar condições

    Dado que a regra "rule-001" está habilitada
    Quando faço POST /api/admin/scheduled-tasks/rules/rule-001/disable
    Então o status de resposta é 200
    E a regra para de avaliar

  Cenário: Avaliar regra manualmente
    Dado que a regra "rule-001" existe
    Quando faço POST /api/admin/scheduled-tasks/rules/rule-001/evaluate
    Então o status de resposta é 200
    E o resultado indica se a condição foi atendida e a ação foi disparada

  Cenário: Deletar regra
    Dado que a regra "rule-001" existe
    Quando faço DELETE /api/admin/scheduled-tasks/rules/rule-001
    Então o status de resposta é 200
    E a regra é removida

  # ══════════════════════════════════════════════
  # CHANNELS & HEALTH
  # ══════════════════════════════════════════════

  Cenário: Listar canais de notificação
    Quando faço GET /api/admin/scheduled-tasks/channels
    Então o status de resposta é 200
    E os canais disponíveis são listados (ex: slack, teams, email)

  Cenário: Testar canal de notificação
    Quando faço POST /api/admin/scheduled-tasks/channels/slack/test
    Então o status de resposta é 200
    E uma mensagem de teste é enviada ao canal

  Cenário: Health check do scheduler (anônimo)
    Quando faço GET /api/admin/scheduled-tasks/health sem autenticação
    Então o status de resposta é 200
    E o status do scheduler é retornado
