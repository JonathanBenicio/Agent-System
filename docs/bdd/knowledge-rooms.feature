Funcionalidade: RAG & Knowledge Rooms
  Como um usuário do sistema
  Quero gerenciar Salas de Conhecimento (Knowledge Rooms)
  Para organizar meus documentos e controlar o acesso dos agentes ao contexto

  Contexto:
    Dado que estou autenticado no sistema
    E o header "X-Tenant-Id" está definido como "default-tenant"

  # ──────────────────────────────────────────────
  # GESTÃO DE SALAS (CRUD)
  # ──────────────────────────────────────────────

  Cenário: Listar salas de conhecimento vazias
    Dado que não existem salas cadastradas para o meu usuário
    Quando faço GET /api/knowledge/rooms
    Então o status de resposta é 200
    E o corpo da resposta deve ser uma lista vazia []

  Cenário: Criar uma nova sala de conhecimento
    Quando faço POST /api/knowledge/rooms com body:
      """json
      {
        "name": "Documentos Jurídicos",
        "description": "Contratos e termos de uso do projeto",
        "color": "bg-blue-500",
        "icon": "FolderOpen",
        "tags": ["Legal", "Internal"]
      }
      """
    Então o status de resposta é 201
    E a sala é criada com um ID único e o campo "documentCount" igual a 0
    E o Location header aponta para a URL da nova sala

  Cenário: Editar metadados de uma sala
    Dado que a sala "Documentos Jurídicos" com ID "room-123" existe
    Quando faço PUT /api/knowledge/rooms/room-123 com body:
      """json
      {
        "id": "room-123",
        "name": "Documentos Jurídicos V2",
        "description": "Versão atualizada dos contratos"
      }
      """
    Então o status de resposta é 200
    E o nome da sala no banco de dados deve ser "Documentos Jurídicos V2"

  Cenário: Excluir uma sala de conhecimento
    Dado que a sala com ID "room-999" existe
    Quando faço DELETE /api/knowledge/rooms/room-999
    Então o status de resposta é 204
    E a sala não deve mais ser retornada na listagem

  # ──────────────────────────────────────────────
  # INGESTÃO E DOCUMENTOS (RAG PIPELINE)
  # ──────────────────────────────────────────────

  Cenário: Ingerir documentos diretamente em uma sala
    Dado que a sala "Fatos do Projeto" com ID "room-rag-01" existe
    Quando envio um arquivo "especificacao.pdf" via POST para /api/document/ingest?roomId=room-rag-01
    Então o documento é processado e vinculado à sala "room-rag-01"
    E o documentCount da sala "room-rag-01" é incrementado para 1

  # ──────────────────────────────────────────────
  # GESTÃO DE PERMISSÕES (ACESS CONTROL)
  # ──────────────────────────────────────────────

  Cenário: Adicionar permissão de acesso para outro usuário
    Dado que sou dono da sala "room-123"
    Quando faço POST /api/knowledge/rooms/room-123/permissions com body:
      """json
      {
        "userId": "colaborador-01",
        "role": "Viewer"
      }
      """
    Então o status de resposta é 200
    E o usuário "colaborador-01" passa a ter acesso de leitura à sala

  Cenário: Negar acesso a sala para usuário sem permissão
    Dado que a sala "room-privada" pertence ao usuário "admin"
    Quando o usuário "invasor" tenta fazer GET /api/knowledge/rooms/room-privada
    Então o status de resposta é 403 Forbidden ou 404 Not Found

  # ──────────────────────────────────────────────
  # INTERFACE (UI FLOWS)
  # ──────────────────────────────────────────────

  Cenário: Visualizar salas na Galeria do Frontend
    Dado que acesso a página "/rag"
    Quando o componente KnowledgeRooms é montado
    Então o sistema chama GET /api/knowledge/rooms
    E exibe um card para cada sala retornada com nome e contagem de documentos

  Cenário: Abrir modal de criação de sala
    Dado que estou na página "/rag"
    Quando clico no botão "New Room"
    Então o modal "Create Knowledge Room" deve ser exibido na tela

  Cenário: Drag and Drop de arquivos para ingestão
    Dado que estou visualizando os detalhes da sala "room-123"
    Quando arrasto um arquivo para a zona de drop
    Então o indicador de "Uploading" deve ficar visível
    E o sistema chama a API de ingestão batch vinculando ao ID da sala
