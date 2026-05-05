# language: pt
Funcionalidade: Backend APIs — Documents, Planner, Voice, Obsidian, Setup
  Como consumidor das APIs do AgenticSystem
  Quero utilizar endpoints de ingestão de documentos, planejamento, voz, Obsidian e setup
  Para interagir com as capacidades especializadas do sistema

  Contexto:
    Dado que estou autenticado via JWT ou API Key

  # ══════════════════════════════════════════════
  # DOCUMENT PIPELINE / RAG (T2)
  # ══════════════════════════════════════════════

  # ──────────────────────────────────────────────
  # Ingestão de documento unitário
  # ──────────────────────────────────────────────

  Cenário: Ingerir documento markdown
    Quando faço POST /api/document/ingest com um arquivo "doc.md" (multipart/form-data)
    Então o status de resposta é 200
    E o documento é processado, chunked e indexado no vector store

  Cenário: Ingerir documento PDF
    Quando faço POST /api/document/ingest com um arquivo "relatorio.pdf"
    Então o status de resposta é 200
    E o conteúdo é extraído, segmentado e indexado

  Cenário: Ingerir imagem para análise visual
    Quando faço POST /api/document/ingest com um arquivo "diagrama.png"
    Então o status de resposta é 200
    E a imagem é processada pelo pipeline de visão (ML26)

  Cenário: Formatos suportados
    Quando faço POST /api/document/ingest com cada tipo de arquivo:
      | Extensão | MIME                                                                 |
      | .md      | text/markdown                                                        |
      | .txt     | text/plain                                                           |
      | .pdf     | application/pdf                                                      |
      | .docx    | application/vnd.openxmlformats-officedocument.wordprocessingml.document |
      | .html    | text/html                                                            |
      | .pptx    | application/vnd.openxmlformats-officedocument.presentationml.presentation |
      | .png     | image/png                                                            |
      | .jpg     | image/jpeg                                                           |
      | .gif     | image/gif                                                            |
      | .webp    | image/webp                                                           |
    Então todos são aceitos com status 200

  Cenário: Rejeitar formato não suportado
    Quando faço POST /api/document/ingest com um arquivo "virus.exe"
    Então o status de resposta é 400
    E a mensagem indica formato não suportado

  # ──────────────────────────────────────────────
  # Ingestão em lote (batch)
  # ──────────────────────────────────────────────

  Cenário: Ingerir múltiplos documentos via batch
    Quando faço POST /api/document/ingest/batch com IFormFileCollection contendo 3 arquivos
    Então o status de resposta é 200
    E todos os documentos são processados

  Cenário: Batch parcialmente falho
    Quando faço POST /api/document/ingest/batch com 3 arquivos (2 válidos + 1 inválido)
    Então o response indica quais foram processados e quais falharam

  # ══════════════════════════════════════════════
  # PLANNER (ML3 — Task Planning)
  # ══════════════════════════════════════════════

  Cenário: Criar plano de execução a partir de objetivo
    Quando faço POST /api/planner/plan com body:
      """json
      { "userId": "user1", "objective": "Migrar banco de dados para PostgreSQL" }
      """
    Então o status de resposta é 200
    E o plano contém etapas decompostas com dependências

  Cenário: Plano requer objetivo não vazio
    Quando faço POST /api/planner/plan com body:
      """json
      { "userId": "user1", "objective": "" }
      """
    Então o status de resposta é 400

  # ══════════════════════════════════════════════
  # VOICE (ML18 — Voice Interface)
  # ══════════════════════════════════════════════

  Cenário: Enviar pergunta por voz (texto)
    Quando faço POST /api/voice/ask com body:
      """json
      { "text": "Qual o status do deploy?", "userId": "user1", "locale": "pt-BR" }
      """
    Então o status de resposta é 200
    E a resposta é em texto plano (markdown removido via StripMarkdown)

  Cenário: Timeout de 7 segundos
    Dado que o agente demora mais de 7 segundos para responder
    Quando faço POST /api/voice/ask
    Então o status de resposta é 200
    E a resposta contém mensagem de timeout padrão

  Cenário: Voice sem userId assume anônimo
    Quando faço POST /api/voice/ask com body:
      """json
      { "text": "Olá", "locale": "pt-BR" }
      """
    Então o status de resposta é 200
    E o userId é tratado como anônimo internamente

  # ══════════════════════════════════════════════
  # OBSIDIAN SYNC (T8)
  # ══════════════════════════════════════════════

  Cenário: Indexar vault Obsidian
    Quando faço POST /api/obsidian/index
    Então o status de resposta é 200
    E os documentos do vault são indexados no vector store

  Cenário: Buscar no vault Obsidian
    Quando faço GET /api/obsidian/search?query=arquitetura
    Então o status de resposta é 200
    E resultados relevantes do vault são retornados

  Cenário: Iniciar watch do vault
    Quando faço POST /api/obsidian/watch/start
    Então o status de resposta é 200
    E mudanças no vault passam a ser detectadas automaticamente

  # ══════════════════════════════════════════════
  # SETUP FLOW (ML15)
  # ══════════════════════════════════════════════

  Cenário: Iniciar fluxo de setup
    Quando faço POST /api/setup/start com body:
      """json
      { "userId": "new-user" }
      """
    Então o status de resposta é 200
    E o primeiro step do onboarding é retornado

  Cenário: Processar step do setup
    Dado que o setup do userId "new-user" está ativo
    Quando faço POST /api/setup/step com body:
      """json
      { "userId": "new-user", "response": "Meu nome é João" }
      """
    Então o status de resposta é 200
    E o próximo step é retornado

  Cenário: Consultar estado atual do setup
    Dado que o setup do userId "new-user" está em progresso
    Quando faço GET /api/setup/state/new-user
    Então o status de resposta é 200
    E o estado atual do setup é retornado (step atual, dados coletados)

  Cenário: Verificar se setup está ativo
    Quando faço GET /api/setup/active/new-user
    Então o status de resposta é 200
    E o response indica se há setup ativo para o usuário

  # ══════════════════════════════════════════════
  # HEALTH & VERSION (Inline — Program.cs)
  # ══════════════════════════════════════════════

  Cenário: Health check anônimo
    Quando faço GET /health sem autenticação
    Então o status de resposta é 200
    E o response contém Status="Healthy" e Timestamp

  Cenário: Version endpoint anônimo
    Quando faço GET /version sem autenticação
    Então o status de resposta é 200
    E o response contém Version e Build
