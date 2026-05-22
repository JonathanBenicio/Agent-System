Funcionalidade: Configurações Avançadas & HotSwap
  Como um administrador do sistema
  Quero gerenciar configurações de baixo nível e trocar implementações em runtime
  Para manter o sistema flexível e seguro sem necessidade de deploys constantes

  Contexto:
    Dado que estou na página de "Configurações Avançadas" (/config/advanced)
    E estou autenticado como administrador

  # ──────────────────────────────────────────────
  # GESTÃO DE CONFIGURAÇÕES (AES-256)
  # ──────────────────────────────────────────────

  Cenário: Criar uma credencial secreta encriptada
    Quando clico em "Nova Config"
    E preencho os campos:
      | Campo       | Valor                   |
      | Chave       | OPENAI_API_KEY          |
      | Valor       | sk-proj-123456...       |
      | Categoria   | Credenciais             |
      | Secreto     | Sim                     |
    E clico em "Criar"
    Então o sistema chama POST /api/admin/config
    E o valor da chave no banco de dados deve estar encriptado via AES-256
    E a interface exibe a chave com um ícone de "Escudo" (Shield)

  Cenário: Listar configurações por categoria
    Quando seleciono a aba "Caminhos" (Paths)
    Então o sistema chama GET /api/admin/config?category=Paths
    E exibe apenas entradas como "CHROMA_DB_PATH" ou "ONNX_MODELS_DIR"

  Cenário: Deletar uma configuração e registrar na auditoria
    Dado que a configuração "OLD_VAR" existe
    Quando clico em "Deletar" na chave "OLD_VAR"
    Então o sistema chama DELETE /api/admin/config/OLD_VAR
    E a chave é removida da listagem
    E um novo registro de ação "Deleted" aparece na aba "Audit Log"

  # ──────────────────────────────────────────────
  # HOT-SWAP (TROCA EM RUNTIME)
  # ──────────────────────────────────────────────

  Cenário: Trocar o Vector Store Provider via Hot-Swap
    Dado que estou na aba "Hot-Swap"
    E o provedor atual de Vector Store é "ChromaDB"
    Quando seleciono "vectorstore" no dropdown de Provedores de Memória
    E clico em "Apply Hot-Swap"
    Então o sistema chama POST /api/admin/config/hot-swap com body:
      """json
      { "subsystem": "vectorstore" }
      """
    E o sistema deve re-instanciar o singleton de memória sem derrubar a aplicação
    E uma notificação de "Hot-Swap aplicado com sucesso" deve ser exibida

  # ──────────────────────────────────────────────
  # AUDITORIA DE ALTERAÇÕES
  # ──────────────────────────────────────────────

  Cenário: Visualizar log de auditoria de alterações
    Quando seleciono a aba "Audit Log"
    Então o sistema chama GET /api/admin/config/audit-log
    E exibe uma tabela com: Data, Chave, Ação (Created/Updated/Deleted) e Hash do valor

  Cenário: Impedir edição da chave (Key) em configurações existentes
    Dado que estou editando a configuração "OPENAI_API_KEY"
    Então o campo "Chave" deve estar desabilitado (disabled) na interface
    Mas o campo "Valor" e "Descrição" podem ser alterados
