# language: pt
Funcionalidade: Alerts History
  Como administrador do AgenticSystem
  Quero visualizar e gerenciar o histórico de alertas do sistema
  Para monitorar falhas, gargalos e anomalias

  Contexto:
    Dado que estou autenticado como administrador

  # ──────────────────────────────────────────────
  # US-38 — Visualizar Histórico e Marcar como Lido
  # ──────────────────────────────────────────────

  Cenário: Listar alertas com paginação
    Dado que acesso a página "/alerts"
    Quando a lista é carregada via GET /api/alerts
    Então vejo a lista de alertas ordenados por data (mais recentes primeiro)
    E cada alerta exibe severidade (Crítico, Aviso, Info), mensagem e timestamp

  Cenário: Marcar alerta como lido
    Dado que estou na página "/alerts"
    E existe um alerta com status "Não Lido"
    Quando clico no botão de check para marcar como lido
    Então o sistema chama POST /api/alerts/{id}/read
    E o alerta muda visualmente para o estado lido (opacidade reduzida)
    E o contador de alertas não lidos é decrementado

  # ──────────────────────────────────────────────
  # US-39 — Geração de Alertas (Simulação de Integração)
  # ──────────────────────────────────────────────

  Cenário: Backend gera alerta por estouro de cota de LLM
    Dado que o backend monitora o consumo de custos e tokens
    Quando um usuário atinge o limite de cota configurado
    Então o sistema gera automaticamente um alerta de severidade "Aviso"
    E a mensagem contém "Limite de cota atingido para o usuário X"
    E o alerta aparece na página "/alerts" após refresh ou via SignalR

  Cenário: Backend gera alerta por falha crítica em cascata de agentes
    Dado que um orquestrador falha após esgotar todas as retentativas de subagentes
    Quando a exceção de cascade failure é lançada
    Então o sistema gera automaticamente um alerta de severidade "Crítico"
    E a mensagem detalha o agente raiz e o motivo da falha
