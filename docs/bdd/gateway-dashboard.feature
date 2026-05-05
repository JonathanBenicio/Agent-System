# language: pt
Funcionalidade: Gateway Dashboard
  Como administrador do AgenticSystem
  Quero visualizar métricas, serviços, saúde e custos do gateway
  Para monitorar a saúde e capacidade do sistema

  Contexto:
    Dado que estou autenticado como administrador
    E a API do gateway está acessível

  # ──────────────────────────────────────────────
  # US-05 — Visualizar métricas do dashboard
  # ──────────────────────────────────────────────

  Cenário: Dashboard exibe cards com métricas consolidadas
    Dado que acesso a página "/dashboard"
    Quando os dados são carregados via GET /api/admin/gateway/dashboard
    Então vejo cards com: Total Agents, Total Tools, Total Plugins, Active Services
    E cada card exibe o valor numérico correspondente

  Cenário: Loading skeleton durante carregamento
    Dado que acesso a página "/dashboard"
    Quando os dados ainda estão sendo carregados
    Então um skeleton loading é exibido nos cards
    E quando os dados chegam, os skeletons são substituídos pelos valores reais

  Cenário: Tratamento de erro com retry no dashboard
    Dado que acesso a página "/dashboard"
    Quando a chamada GET /api/admin/gateway/dashboard falha
    Então uma mensagem de erro é exibida
    E um botão "Retry" está disponível
    E ao clicar em "Retry", os dados são recarregados

  # ──────────────────────────────────────────────
  # US-06 — Listar serviços do gateway
  # ──────────────────────────────────────────────

  Cenário: Tabela de serviços com status
    Dado que acesso a página "/services"
    Quando os serviços são carregados via GET /api/admin/gateway/services
    Então uma tabela exibe nome, status, categoria e toggle enable/disable
    E cada serviço possui um toggle de ativação

  Cenário: Filtrar serviços por categoria
    Dado que estou na página "/services"
    E existem serviços das categorias "LLM", "Storage" e "Monitoring"
    Quando seleciono o filtro de categoria "LLM"
    Então apenas serviços da categoria "LLM" são exibidos

  Cenário: Habilitar serviço desabilitado
    Dado que o serviço "OpenAI" está desabilitado
    Quando clico no toggle de ativação do serviço "OpenAI"
    Então o sistema chama POST /api/admin/gateway/services/OpenAI/enable
    E o status do serviço muda para "Enabled"
    E um toast de sucesso é exibido

  Cenário: Desabilitar serviço habilitado
    Dado que o serviço "Ollama" está habilitado
    Quando clico no toggle de desativação do serviço "Ollama"
    Então o sistema chama POST /api/admin/gateway/services/Ollama/disable
    E o status do serviço muda para "Disabled"

  Cenário: Serviço não encontrado retorna 404
    Quando faço GET /api/admin/gateway/services/inexistente
    Então o status de resposta é 404
    E o body contém mensagem de erro indicando serviço não encontrado

  # ──────────────────────────────────────────────
  # US-07 — Monitorar saúde dos serviços
  # ──────────────────────────────────────────────

  Cenário: Status geral de saúde
    Dado que acesso a página "/health"
    Quando os dados são carregados via GET /api/admin/gateway/health
    Então o status geral é exibido: "Healthy", "Degraded" ou "Unhealthy"
    E cada serviço possui seu health check individual

  Cenário: Cores semafóricas por status
    Dado que estou na página "/health"
    Quando há serviços com diferentes status de saúde
    Então serviços "Healthy" exibem indicador verde
    E serviços "Degraded" exibem indicador amarelo
    E serviços "Unhealthy" exibem indicador vermelho

  Cenário: Timestamp da última verificação
    Dado que estou na página "/health"
    Quando vejo os health checks dos serviços
    Então cada check exibe o timestamp da última verificação

  # ──────────────────────────────────────────────
  # US-08 — Consultar custos por provider
  # ──────────────────────────────────────────────

  Cenário: Breakdown de custos por provider
    Dado que acesso a página "/costs"
    Quando os dados são carregados via GET /api/admin/gateway/costs
    Então o custo total é exibido
    E um breakdown por provider é listado
    E valores são formatados em moeda

  Cenário: Budget diário e alerta de proximidade
    Dado que estou na página "/costs"
    E o budget diário é de R$ 100
    Quando o custo acumulado atinge R$ 85 (85%)
    Então um alerta visual indica proximidade do limite
