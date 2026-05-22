# language: pt
Funcionalidade: Chat Interface
  Como usuário do AgenticSystem
  Quero enviar mensagens e receber respostas em tempo real
  Para interagir com os agentes de IA

  Contexto:
    Dado que estou autenticado no sistema
    E a conexão SignalR com o ChatHub está estabelecida

  # ──────────────────────────────────────────────
  # US-01 — Enviar mensagem de texto
  # ──────────────────────────────────────────────

  Cenário: Selecionar provider e modelo antes do envio
    Dado que estou na página "/"
    E existe ao menos um provider habilitado na configuração de IA
    Quando seleciono o provider "OpenAI"
    E seleciono o modelo "gpt-4o-mini"
    Então o chat mantém essa seleção como contexto da conversa atual
    E o próximo envio inclui provider e model na chamada para o backend

  Cenário: Acessar configuração de IA a partir do chat
    Dado que estou na página "/"
    Quando clico na ação "Configurar IA"
    Então sou redirecionado para a rota "/ai"

  Cenário: Enviar mensagem via campo de input
    Dado que estou na página "/"
    E o campo de input está vazio
    Quando digito "Qual o status dos serviços?" no campo de input
    E pressiono Enter
    Então a mensagem "Qual o status dos serviços?" aparece no chat como mensagem do usuário
    E o campo de input é limpo
    E o indicador de "digitando" é exibido

  Cenário: Enviar mensagem com Shift+Enter insere nova linha
    Dado que estou na página "/"
    Quando digito "Primeira linha" no campo de input
    E pressiono Shift+Enter
    E digito "Segunda linha"
    Então o campo de input contém texto com duas linhas
    E a mensagem NÃO é enviada

  Cenário: Rate limiting de 500ms entre envios
    Dado que estou na página "/"
    E acabei de enviar uma mensagem
    Quando tento enviar outra mensagem imediatamente (< 500ms)
    Então a segunda mensagem NÃO é enviada
    E o botão de envio está desabilitado temporariamente

  Cenário: Guard contra envio duplo
    Dado que estou na página "/"
    E uma mensagem está sendo processada
    Quando clico no botão de enviar novamente rapidamente
    Então apenas uma mensagem é enviada ao servidor
    E o envio duplicado é bloqueado via sendingRef

  Cenário: Fallback REST quando SignalR indisponível
    Dado que estou na página "/"
    E a conexão SignalR está desconectada
    Quando envio a mensagem "Teste fallback"
    Então a mensagem é enviada via POST /api/chat
    E a resposta é exibida normalmente no chat

  # ──────────────────────────────────────────────
  # US-02 — Receber resposta do agente em tempo real
  # ──────────────────────────────────────────────

  Cenário: Resposta renderizada com Markdown
    Dado que enviei a mensagem "Explique Clean Architecture"
    Quando o agente responde com conteúdo Markdown (headers, código, listas)
    Então a resposta é renderizada com formatação Markdown via react-markdown
    E blocos de código possuem syntax highlighting

  Cenário: Proteção XSS na renderização
    Dado que enviei uma mensagem
    Quando o agente responde com conteúdo contendo tags "<script>alert('xss')</script>"
    Então as tags script/iframe/object/embed/form são removidas
    E nenhum código JavaScript é executado no browser

  Cenário: Auto-scroll para última mensagem
    Dado que o chat possui muitas mensagens e está scrollado para cima
    Quando uma nova resposta do agente chega
    Então o chat faz auto-scroll para a última mensagem
    E a resposta mais recente fica visível

  Cenário: Indicador de digitando durante processamento
    Dado que enviei a mensagem "Analise o dashboard"
    Quando o evento SignalR "ProcessingStarted" é recebido
    Então o indicador de "digitando" (typing animation) é exibido
    E quando o evento "ReceiveMessage" chega, o indicador desaparece

  # ──────────────────────────────────────────────
  # US-03 — Identificar agente que respondeu
  # ──────────────────────────────────────────────

  Cenário: Badge com tier do agente na resposta
    Dado que enviei uma mensagem
    Quando o agente "AnalysisAgent" (tier 2 — Specialist) responde
    Então a mensagem exibe o nome "AnalysisAgent"
    E um badge colorido com label "Specialist"
    E a cor do badge corresponde ao tier 2

  Cenário: Cores diferenciadas por tier
    Dado que há respostas de agentes de diferentes tiers
    Então o tier 0 (Chief) exibe badge com cor distinta
    E o tier 1 (Master) exibe badge com cor diferente do Chief
    E o tier 2 (Specialist) exibe badge com cor diferente do Master
    E o tier 3 (Support) exibe badge com cor diferente do Specialist

  # ──────────────────────────────────────────────
  # US-04 — Gerenciar sessões de chat e Roteamento Semântico
  # ──────────────────────────────────────────────

  Cenário: Nova sessão com ID seguro
    Dado que estou na página "/"
    Quando inicio uma nova sessão
    Então um ID de sessão é gerado com crypto.randomUUID()
    E a sessão é registrada via API /api/sessions

  Cenário: Histórico de mensagens por sessão
    Dado que tenho uma sessão ativa com 5 mensagens
    Quando fecho e reabro a página
    Então as 5 mensagens anteriores são carregadas da API
    E a ordem cronológica é mantida

  Cenário: Triagem na borda e curto-circuito de baixa complexidade
    Dado que envio a mensagem simples "Qual a data de hoje?"
    Quando o serviço de triagem classifica o prompt como complexidade baixa e intenção de resposta direta
    Então a requisição é interceptada pelo DirectAgentRequestExecutor
    E a resposta é gerada em fast-path sem acionar orquestradores complexos

  Cenário: Consolidação e compactação de histórico de longo prazo
    Dado que uma sessão ativa ultrapassa o limite configurado de mensagens
    Quando o SessionConsolidator é acionado em background
    Então as mensagens antigas são compactadas em um resumo semântico gerado por LLM
    E o contexto essencial da conversa é preservado sem estourar a janela de tokens

  # ──────────────────────────────────────────────
  # US-11 — Ingestão RAG via Drag and Drop
  # ──────────────────────────────────────────────

  Cenário: Exibir overlay visual ao arrastar arquivo sobre o chat
    Dado que estou na página "/"
    Quando arrasto um arquivo válido para a área de chat
    Então o overlay de "Ingestão RAG Contextual" é exibido com desfoque de fundo e borda tracejada
    E exibe o modo atual de indexação (Sala ou Sessão)

  Cenário: Realizar ingestão bem-sucedida de documentos
    Dado que estou na página "/"
    Quando solto os arquivos "documento.pdf" na área de chat
    Então exibe um spinner com o texto "Processando Documentos"
    E os arquivos são ingeridos via API POST /api/document/ingest
    E uma notificação de sucesso em Toast é exibida
    E uma mensagem de sistema é adicionada informando que os arquivos foram indexados com sucesso

  # ──────────────────────────────────────────────
  # US-12 — Rastreabilidade e Citações de Fontes RAG
  # ──────────────────────────────────────────────

  Cenário: Visualizar fontes de RAG na mensagem de resposta
    Dado que o agente responde a uma pergunta baseada em documentos
    Quando a resposta do agente contendo citações é renderizada
    Então exibe uma seção de "Fontes" com a contagem de referências utilizadas
    E cada fonte exibe o nome abreviado do documento de origem

  Cenário: Interagir com o popover de citação
    Dado que a resposta exibe fontes de RAG
    Quando clico na pílula da primeira fonte bibliográfica
    Então exibe um painel popup contendo o trecho exato do texto citado
    E exibe a porcentagem de confiança da citação
    E exibe o número da página se disponível

  # ──────────────────────────────────────────────
  # US-13 — Visualização de Workflows, Ações e Metadados
  # ──────────────────────────────────────────────

  Cenário: Exibição de card interativo de execução de Workflow
    Dado que o agente dispara um fluxo operacional em background
    Quando a mensagem recebida possui um ID de execução de workflow
    Então exibe um cartão interativo de execução do Workflow na linha do tempo
    E oculta a mensagem padrão de texto do agente

  Cenário: Visualizar badges de ferramentas e ações executadas
    Dado que o agente utilizou recursos externos para responder
    Quando a mensagem de resposta é renderizada
    Então exibe um badge identificador "⚡ [Nome da Ação]" para cada ação acionada
    E exibe um badge identificador "🔧 [Nome da Ferramenta]" para cada ferramenta utilizada

  Cenário: Exibição de indicador de memória recuperada
    Dado que o usuário envia um prompt que altera/ativa memórias anteriores
    Quando a mensagem do usuário é renderizada na timeline
    Então exibe um badge verde sutil de "Memória Recuperada" com ícone de robô acima do texto

  Cenário: Exibição de mensagem de alerta do sistema
    Dado que ocorre uma falha sistêmica ou de conexão na comunicação
    Quando uma mensagem com o papel (role) de "system" é inserida na timeline
    Então ela é formatada com borda avermelhada e ícone de triângulo de alerta
    E exibe a descrição do erro de forma amigável

