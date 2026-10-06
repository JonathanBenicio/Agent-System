# language: pt
Funcionalidade: Chat Avançado e Gerenciamento Unificado de Sessões
  Como um usuário autenticado no sistema
  Eu quero gerenciar sessões, filtrar RAG por salas de conhecimento, enviar arquivos e disparar workflows pelo chat
  Para ter um ambiente de trabalho centralizado e produtivo

  Contexto:
    Dado que estou autenticado na plataforma no tenant "tenant-empresa"
    E possuo acesso ao hub SignalR "/hubs/chat" e "/hubs/workflow"

  Cenário: Continuidade e persistência de conversa em uma sessão de chat existente
    Dado que possuo uma sessão de chat existente com o ID "sessao-historica-123"
    E a sessão possui 4 mensagens anteriores salvas no banco de dados
    Quando eu envio a mensagem "Explique o relatório consolidado" com o "sessionId" igual a "sessao-historica-123"
    Então o sistema deve processar a mensagem no contexto da sessão "sessao-historica-123"
    E não deve criar uma nova sessão no banco de dados
    E deve carregar as mensagens históricas para compor a memória de contexto do LLM

  Cenário: Busca semântica (RAG) restrita por Sala de Conhecimento selecionada (Estilo NotebookLM)
    Dado que selecionei a Sala de Conhecimento "Manual de Engenharia" com ID "sala-eng-456" no chat
    Quando eu envio o prompt "Qual o torque recomendado para o motor?"
    Então o pipeline RAG deve interceptar o "KnowledgeRoomId" igual a "sala-eng-456"
    E deve invocar a busca vetorial aplicando o filtro "room_ids" igual a "sala-eng-456"
    E deve injetar no prompt do LLM apenas os trechos de documentos pertencentes à sala "sala-eng-456"

  Cenário: Upload de arquivos isolados restritos ao escopo da sessão do chat
    Dado que não tenho nenhuma Sala de Conhecimento selecionada no chat
    E estou conversando na sessão de chat ativa "chat-ativo-789"
    Quando eu faço o upload do arquivo "contrato_prestacao.pdf" através da tela de chat
    Então o sistema deve ingerir o documento com o parâmetro "source" igual a "chat-ativo-789"
    E deve criar os chunks e embeddings correspondentes com a "Collection" igual a "chat-ativo-789"
    E qualquer busca RAG subsequente nesta sessão "chat-ativo-789" deve poder ler os dados do arquivo "contrato_prestacao.pdf"
    Mas buscas RAG em outras sessões ou salas não devem ter acesso aos dados deste arquivo

  Cenário: Seleção e disparo de execução de Workflow a partir do chat
    Dado que visualizo a lista de Workflows disponíveis no chat
    Quando eu seleciono o workflow "Análise de Riscos Financeiros" com ID "wf-finance-001"
    E clico em "Executar Workflow"
    Então o frontend deve disparar um POST para "/api/workflow/executions/start/wf-finance-001"
    E deve inserir um card de progresso "WorkflowExecutionCard" com status "Running" na timeline do chat
    E o card deve escutar os eventos do hub "/hubs/workflow" para atualizar as etapas da execução em tempo real
