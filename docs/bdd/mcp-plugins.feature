# language: pt
Funcionalidade: MCP Plugins
  Como administrador do AgenticSystem
  Quero gerenciar MCP Plugins (carregar, listar, executar tools, acessar resources)
  Para estender o sistema com ferramentas externas via Model Context Protocol

  Contexto:
    Dado que estou autenticado como administrador

  # ──────────────────────────────────────────────
  # US-20 — Listar plugins carregados
  # ──────────────────────────────────────────────

  Cenário: Grid de plugins com status
    Dado que acesso a página "/plugins"
    Quando os dados são carregados via GET /api/admin/plugins
    Então um grid de plugins é exibido
    E cada card mostra nome, status e quantidade de tools

  Cenário: Status geral dos plugins
    Quando faço GET /api/admin/plugins/status
    Então o status de resposta é 200
    E o response contém o resumo de plugins carregados e com erro

  # ──────────────────────────────────────────────
  # US-21 — Carregar novo plugin
  # ──────────────────────────────────────────────

  Cenário: Carregar plugin via formulário
    Dado que estou na página "/plugins"
    Quando clico em "Carregar Plugin"
    E preencho: Name "filesystem", Command "npx @modelcontextprotocol/server-filesystem"
    E confirmo o carregamento
    Então o sistema chama POST /api/admin/plugins/load com MCPPluginConfig
    E um toast de sucesso é exibido
    E o plugin aparece na lista

  Cenário: Carregar plugin HTTP
    Dado que estou na página "/plugins"
    Quando carrego um plugin com endpoint "http://localhost:3001/mcp"
    Então o sistema aceita plugins via HTTP/SSE
    E o plugin é listado com status ativo

  Cenário: Erro ao carregar plugin inválido
    Dado que estou na página "/plugins"
    Quando carrego um plugin com comando inválido "nao-existe-servidor"
    Então um toast de erro é exibido
    E o plugin NÃO é adicionado à lista

  Cenário: Remover plugin carregado
    Dado que o plugin "filesystem" está carregado com id "fs-001"
    Quando faço DELETE /api/admin/plugins/fs-001
    Então o status de resposta é 200
    E o plugin é removido da lista ativa

  # ──────────────────────────────────────────────
  # US-22 — Detalhes do plugin e ferramentas
  # ──────────────────────────────────────────────

  Cenário: Ver detalhes do plugin
    Dado que o plugin "filesystem" está carregado com id "fs-001"
    Quando clico no card do plugin "filesystem"
    Então o modal de detalhes exibe: nome, comando/endpoint, status
    E a lista de tools disponíveis é exibida
    E a lista de resources é exibida

  Cenário: Listar tools de todos os plugins
    Quando faço GET /api/admin/plugins/tools
    Então o status de resposta é 200
    E tools de todos os plugins carregados são retornadas

  Cenário: Executar tool de plugin
    Dado que o plugin "fs-001" possui a tool "read_file"
    Quando faço POST /api/admin/plugins/fs-001/tools/read_file/execute com body:
      """json
      { "path": "/tmp/test.txt" }
      """
    Então o status de resposta é 200
    E o resultado da execução é retornado

  Cenário: Listar resources do plugin
    Dado que o plugin "fs-001" está carregado
    Quando faço GET /api/admin/plugins/fs-001/resources
    Então o status de resposta é 200
    E os resources disponíveis são listados

  Cenário: Acessar resource específico
    Dado que o plugin "fs-001" possui resource "file:///data/config.json"
    Quando faço GET /api/admin/plugins/fs-001/resources/file%3A%2F%2F%2Fdata%2Fconfig.json
    Então o status de resposta é 200
    E o conteúdo do resource é retornado
