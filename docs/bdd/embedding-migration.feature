# language: pt
Funcionalidade: Embedding Migration
  Como administrador do AgenticSystem
  Quero gerenciar modelos de embedding e jobs de migração
  Para atualizar embeddings sem downtime e manter qualidade semântica

  Contexto:
    Dado que estou autenticado como administrador

  # ══════════════════════════════════════════════
  # MODELS CRUD (ML23)
  # ══════════════════════════════════════════════

  Cenário: Listar modelos de embedding disponíveis
    Quando faço GET /api/admin/embedding-migration/models
    Então o status de resposta é 200
    E a lista de modelos configurados é retornada

  Cenário: Consultar modelo por ID
    Dado que o modelo "model-001" existe
    Quando faço GET /api/admin/embedding-migration/models/model-001
    Então o status de resposta é 200
    E os detalhes são retornados (Name, Provider, ModelName, Dimensions, IsActive)

  Cenário: Consultar modelo ativo
    Quando faço GET /api/admin/embedding-migration/models/active
    Então o status de resposta é 200
    E o modelo com IsActive=true é retornado

  Cenário: Cadastrar novo modelo de embedding
    Quando faço POST /api/admin/embedding-migration/models com body:
      """json
      {
        "name": "text-embedding-3-large",
        "provider": "OpenAI",
        "modelName": "text-embedding-3-large",
        "dimensions": 3072,
        "baseUrl": "https://api.openai.com",
        "apiKey": "sk-xxx",
        "isActive": false
      }
      """
    Então o status de resposta é 200
    E o modelo é cadastrado (inativo por padrão)

  Cenário: Ativar modelo
    Dado que o modelo "model-002" existe e está inativo
    Quando faço POST /api/admin/embedding-migration/models/model-002/activate
    Então o status de resposta é 200
    E o modelo "model-002" passa a ser o ativo
    E o modelo anteriormente ativo é desativado

  Cenário: Deletar modelo não ativo
    Dado que o modelo "model-003" existe e NÃO está ativo
    Quando faço DELETE /api/admin/embedding-migration/models/model-003
    Então o status de resposta é 200
    E o modelo é removido

  Cenário: Impedir deleção de modelo ativo
    Dado que o modelo "model-001" é o modelo ativo
    Quando faço DELETE /api/admin/embedding-migration/models/model-001
    Então o status de resposta é 400
    E a mensagem indica que não é possível deletar o modelo ativo

  # ══════════════════════════════════════════════
  # MIGRATION JOBS
  # ══════════════════════════════════════════════

  Cenário: Criar job de migração
    Quando faço POST /api/admin/embedding-migration/jobs com body:
      """json
      {
        "sourceModelId": "model-001",
        "targetModelId": "model-002",
        "batchSize": 100
      }
      """
    Então o status de resposta é 200
    E um job de migração é criado com status "Pending"

  Cenário: Listar jobs de migração
    Quando faço GET /api/admin/embedding-migration/jobs
    Então o status de resposta é 200
    E a lista de jobs é retornada com status de cada um

  Cenário: Consultar job por ID
    Dado que o job "job-001" existe
    Quando faço GET /api/admin/embedding-migration/jobs/job-001
    Então o status de resposta é 200
    E os detalhes do job são retornados

  Cenário: Consultar status detalhado do job
    Dado que o job "job-001" está em progresso
    Quando faço GET /api/admin/embedding-migration/jobs/job-001/status
    Então o status de resposta é 200
    E o response contém: progresso (%), documentos processados, erros, tempo estimado

  Cenário: Cancelar job em andamento
    Dado que o job "job-001" está em execução
    Quando faço POST /api/admin/embedding-migration/jobs/job-001/cancel
    Então o status de resposta é 200
    E o job é cancelado graciosamente

  Cenário: Retry de job com falha
    Dado que o job "job-001" falhou
    Quando faço POST /api/admin/embedding-migration/jobs/job-001/retry
    Então o status de resposta é 200
    E o job reinicia a partir do último ponto de falha

  Cenário: Switchover após migração completa
    Dado que o job "job-001" completou com 100% de sucesso
    Quando faço POST /api/admin/embedding-migration/jobs/job-001/switch
    Então o status de resposta é 200
    E o modelo target é ativado
    E o sistema passa a usar o novo embedding
