Funcionalidade: ONNX Model Management (UI)
  Como um administrador do sistema
  Quero gerenciar modelos ONNX através da interface
  Para habilitar e testar capacidades de inferência local (IA Generativa e Visão)

  Contexto:
    Dado que estou na página de "Modelos IA" (/onnx)
    E estou autenticado como administrador

  # ──────────────────────────────────────────────
  # CICLO DE VIDA DO MODELO (CRUD & STATUS)
  # ──────────────────────────────────────────────

  Cenário: Realizar upload de um novo modelo ONNX
    Quando clico no botão "Upload Modelo"
    E seleciono o arquivo "fast-super-res.onnx"
    E preencho os metadados:
      | Campo         | Valor                   |
      | Name          | Super Resolution X4     |
      | Description   | Modelo de upscaling 4x  |
      | Input Width   | 256                     |
      | Input Height  | 256                     |
      | Channels      | 3                       |
    E clico em "Enviar"
    Então o sistema chama POST /api/onnx/models
    E o modelo aparece na lista com status "Inativo"

  Cenário: Ativar um modelo para inferência
    Dado que o modelo "Super Resolution X4" está inativo
    Quando clico no botão "Ativar" no card do modelo
    Então o sistema chama PUT /api/onnx/models/{id} com isActive=true
    E o indicador de status do modelo muda para verde (Ativo)

  Cenário: Editar metadados técnicos de um modelo
    Dado que o modelo com ID "model-001" existe
    Quando clico em "Editar" no modelo "model-001"
    E altero o "Output Format" para "Image"
    E salvo as alterações
    Então o sistema chama PUT /api/onnx/models/model-001
    E a alteração é refletida no chip de metadados do card

  Cenário: Deletar modelo com confirmação
    Quando clico no botão "Deletar" do modelo "Super Resolution X4"
    Então um modal de confirmação deve ser exibido
    Quando confirmo a exclusão
    Então o sistema chama DELETE /api/onnx/models/{id}
    E o modelo é removido da listagem

  # ──────────────────────────────────────────────
  # INSPEÇÃO E TESTE
  # ──────────────────────────────────────────────

  Cenário: Inspecionar estrutura interna do modelo (Tensores)
    Quando clico em "Inspecionar" no modelo "Super Resolution X4"
    Então o modal "Inspecionar Modelo" é exibido
    E o sistema chama POST /api/onnx/models/{id}/inspect
    E exibe a lista de Inputs e Outputs (Nomes, Tipos e Dimensões) extraídos do arquivo .onnx

  Cenário: Disparar inferência de teste manual
    Dado que estou no modal "Testar Modelo" para o modelo "Super Resolution X4"
    Quando seleciono uma imagem de input
    E clico em "Executar Teste"
    Então o sistema chama POST /api/onnx/models/{id}/test (Multipart)
    E exibe um indicador de processamento
    E redireciona ou sugere visualizar o resultado na "Galeria"

  # ──────────────────────────────────────────────
  # GALERIA DE RESULTADOS
  # ──────────────────────────────────────────────

  Cenário: Visualizar histórico de inferências na galeria
    Quando mudo para a aba "Galeria de Resultados"
    Então o sistema chama GET /api/onnx/models/jobs
    E exibe um grid com as imagens geradas por inferências anteriores
    E cada item mostra o nome do modelo utilizado e o timestamp
