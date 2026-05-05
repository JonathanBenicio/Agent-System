# language: pt
Funcionalidade: Settings e Configuration
  Como administrador do AgenticSystem
  Quero configurar parâmetros do gateway, memória e configurações avançadas
  Para ajustar o comportamento global do sistema

  Contexto:
    Dado que estou autenticado como administrador

  # ──────────────────────────────────────────────
  # US-17 — Configurar parâmetros do gateway
  # ──────────────────────────────────────────────

  Cenário: Visualizar configurações do gateway
    Dado que acesso a página "/settings"
    E a tab "Gateway" está ativa
    Quando os dados são carregados via GET /api/admin/settings/gateway
    Então os campos são exibidos:
      | Campo                       | Tipo    |
      | DefaultDailyBudget          | decimal |
      | DefaultFailureThreshold     | inteiro |
      | DefaultBreakDurationSeconds | inteiro |
      | DefaultRequestsPerMinute    | inteiro |

  Cenário: Salvar configurações do gateway
    Dado que estou na tab "Gateway" da página "/settings"
    Quando altero DefaultRequestsPerMinute para 120
    E clico em "Salvar"
    Então o sistema chama PUT /api/admin/settings/gateway com os valores atualizados
    E um toast de confirmação é exibido

  # ──────────────────────────────────────────────
  # US-18 — Configurar parâmetros de memória
  # ──────────────────────────────────────────────

  Cenário: Visualizar configurações de memória
    Dado que acesso a página "/settings"
    Quando clico na tab "Memory"
    E os dados são carregados via GET /api/admin/settings/memory
    Então os campos são exibidos:
      | Campo             | Tipo   |
      | ObsidianVaultPath | string |
      | VectorStoreType   | string |
      | ConnectionString  | string |

  Cenário: Salvar configurações de memória
    Dado que estou na tab "Memory" da página "/settings"
    Quando altero VectorStoreType para "PostgreSQL"
    E clico em "Salvar"
    Então o sistema chama PUT /api/admin/settings/memory com os valores atualizados
    E um toast de confirmação é exibido

  # ──────────────────────────────────────────────
  # US-19 — Alternar entre tabs de configuração
  # ──────────────────────────────────────────────

  Cenário: Navegar entre tabs Gateway e Memory
    Dado que estou na página "/settings" com tab "Gateway" ativa
    Quando clico na tab "Memory"
    Então a tab "Memory" fica ativa
    E o conteúdo muda para configurações de memória
    E a tab "Gateway" fica inativa

  Cenário: Estado da tab ativa persiste na sessão
    Dado que estou na tab "Memory" da página "/settings"
    Quando navego para outra página e retorno
    Então a tab "Memory" continua ativa

  # ──────────────────────────────────────────────
  # Settings API — Endpoints adicionais
  # ──────────────────────────────────────────────

  Cenário: Consultar todas as settings consolidadas
    Quando faço GET /api/admin/settings
    Então o status de resposta é 200
    E o response contém seções: openAI, ollama, gemini, claude, gateway, memory
    E campos de API Key retornam hasApiKey (booleano) em vez do valor

  Cenário: Consultar settings de providers
    Quando faço GET /api/admin/settings/providers
    Então o status de resposta é 200
    E informações dos providers LLM são retornadas

  # ──────────────────────────────────────────────
  # Config Management (Avançado — ML22)
  # ──────────────────────────────────────────────

  Cenário: Listar configurações por categoria
    Quando faço GET /api/admin/config?category=Credentials
    Então o status de resposta é 200
    E apenas entradas da categoria "Credentials" são retornadas

  Cenário: Criar configuração
    Quando faço POST /api/admin/config com body:
      """json
      { "key": "app:feature-flag", "value": "true", "category": "General", "isSecret": false }
      """
    Então o status de resposta é 200
    E a configuração é persistida

  Cenário: Atualizar configuração existente
    Dado que a configuração "app:feature-flag" existe
    Quando faço PUT /api/admin/config/app:feature-flag com novo valor "false"
    Então o status de resposta é 200
    E o valor é atualizado

  Cenário: Deletar configuração
    Dado que a configuração "app:temp-setting" existe
    Quando faço DELETE /api/admin/config/app:temp-setting
    Então o status de resposta é 200
    E a configuração não existe mais

  Cenário: Validar configuração
    Dado que a configuração "db:connection" existe
    Quando faço GET /api/admin/config/db:connection/validate
    Então o status de resposta é 200
    E o resultado indica se a configuração é válida

  Cenário: Audit log de configurações
    Dado que configurações foram criadas, atualizadas e deletadas
    Quando faço GET /api/admin/config/audit-log?limit=50
    Então o status de resposta é 200
    E o log contém registros de cada operação com timestamp
    E o limite de registros respeita o parâmetro (máx 500)

  Cenário: Secret nunca retorna plaintext
    Dado que existe uma configuração com isSecret=true e valor "senha123"
    Quando faço GET /api/admin/config/secret-key
    Então o valor retornado é "********" (mascarado)
    E o valor real NUNCA é exposto na API
