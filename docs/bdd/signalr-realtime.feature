# language: pt
Funcionalidade: SignalR Real-time
  Como usuário do AgenticSystem
  Quero receber atualizações em tempo real via WebSocket (SignalR)
  Para acompanhar respostas de chat e mudanças no gateway sem polling

  Contexto:
    Dado que estou autenticado
    E o navegador suporta WebSocket

  # ──────────────────────────────────────────────
  # US-23 — Conexão ChatHub e streaming de respostas
  # ──────────────────────────────────────────────

  Cenário: Conectar ao ChatHub
    Dado que acesso a página de chat
    Quando a página é carregada
    Então uma conexão SignalR é estabelecida em /hubs/chat
    E o status do hub indica "Connected"

  Cenário: Receber resposta do agente via streaming
    Dado que estou conectado ao ChatHub
    Quando envio uma mensagem via hub.invoke("SendMessage", message, targetAgent)
    Então recebo eventos de streaming em tempo real
    E cada chunk é renderizado progressivamente no chat

  Cenário: Reconexão automática após desconexão
    Dado que estou conectado ao ChatHub
    Quando a conexão é interrompida temporariamente
    Então o hub tenta reconectar automaticamente
    E o status volta para "Connected" após reconexão

  Cenário: Autenticação obrigatória no ChatHub
    Dado que NÃO estou autenticado
    Quando tento conectar ao /hubs/chat
    Então a conexão é rejeitada com 401 Unauthorized

  # ──────────────────────────────────────────────
  # US-24 — Conexão GatewayHub para métricas em tempo real
  # ──────────────────────────────────────────────

  Cenário: Conectar ao GatewayHub
    Dado que acesso o dashboard administrativo
    Quando a página é carregada
    Então uma conexão SignalR é estabelecida em /hubs/gateway
    E o hub fica pronto para receber eventos

  Cenário: Receber atualização de métricas do gateway
    Dado que estou conectado ao GatewayHub
    Quando métricas são atualizadas no backend
    Então recebo eventos com dados atualizados
    E os cards do dashboard refletem os novos valores sem refresh

  Cenário: Receber notificação de serviço degradado
    Dado que estou conectado ao GatewayHub
    Quando o health check de um serviço muda para "Degraded"
    Então recebo um evento com a mudança de status
    E o indicador visual do serviço muda para amarelo

  Cenário: Autenticação obrigatória no GatewayHub
    Dado que NÃO estou autenticado
    Quando tento conectar ao /hubs/gateway
    Então a conexão é rejeitada com 401 Unauthorized

  # ──────────────────────────────────────────────
  # US-25 — Indicador de processamento
  # ──────────────────────────────────────────────

  Cenário: Indicador visual enquanto agente processa
    Dado que estou no chat e enviei uma mensagem
    Quando o agente está processando a resposta
    Então um indicador de "digitando" é exibido
    E o indicador mostra qual agente está respondendo

  Cenário: Indicador desaparece ao receber resposta completa
    Dado que o indicador de processamento está visível
    Quando a resposta completa do agente é recebida
    Então o indicador desaparece
    E a mensagem completa é exibida no chat

  Cenário: Indicador com timeout
    Dado que enviei uma mensagem
    Quando o agente não responde em 30 segundos
    Então o indicador de processamento exibe um aviso de timeout
    E o usuário pode optar por cancelar ou aguardar
