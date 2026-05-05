# language: pt
Funcionalidade: LLM Providers Management
  Como administrador do AgenticSystem
  Quero gerenciar providers de LLM (OpenAI, Ollama, Gemini, Claude)
  Para controlar quais modelos estão disponíveis, definir a IA default do chat e validar conectividade

  Contexto:
    Dado que estou autenticado como administrador

  # ──────────────────────────────────────────────
  # US-15 — Gerenciar providers de LLM
  # ──────────────────────────────────────────────

  Cenário: Acessar a área dedicada de configuração de IAs
    Dado que acesso a página "/ai"
    Quando os dados são carregados via GET /api/admin/llm/configuration
    Então uma lista de providers é exibida
    E cada provider mostra: nome, modelo default, prioridade e status (enabled/disabled)
    E a tela exibe o provider e o modelo default usados como seleção inicial do chat

  Cenário: Redirecionar rota legada de providers
    Dado que acesso a rota "/providers"
    Quando o frontend inicializa o router
    Então sou redirecionado para a rota "/ai"

  Cenário: Listar apenas providers habilitados
    Quando faço GET /api/admin/llm/providers/enabled
    Então o status de resposta é 200
    E apenas providers com Enabled=true são retornados

  Cenário: Consultar provider default
    Quando faço GET /api/admin/llm/providers/default
    Então o status de resposta é 200
    E o provider retornado é o de maior prioridade entre os habilitados

  Cenário: Consultar provider específico
    Quando faço GET /api/admin/llm/providers/openai
    Então o status de resposta é 200
    E os detalhes do provider "openai" são retornados

  Cenário: Editar configuração do provider
    Dado que o provider "ollama" existe
    Quando faço PUT /api/admin/llm/providers/ollama com body:
      """json
      { "apiKey": null, "defaultModel": "llama3", "enabled": true, "priority": 2 }
      """
    Então o status de resposta é 200
    E o modelo default do "ollama" é atualizado para "llama3"

  Cenário: Definir a IA padrão do chat
    Dado que o provider "openai" está habilitado
    Quando faço PUT /api/admin/llm/default-selection com body:
      """json
      { "providerName": "openai", "model": "gpt-4o-mini" }
      """
    Então o status de resposta é 200
    E o provider default da configuração passa a ser "openai"
    E o modelo default da configuração passa a ser "gpt-4o-mini"

  Cenário: API Key é mascarada no response
    Dado que o provider "openai" possui API Key configurada
    Quando faço GET /api/admin/llm/providers/openai
    Então o campo hasApiKey indica true
    E o valor da API Key NÃO é retornado em plaintext

  # ──────────────────────────────────────────────
  # US-16 — Testar conexão com provider
  # ──────────────────────────────────────────────

  Cenário: Testar conexão com sucesso
    Dado que o provider "openai" está habilitado e configurado
    Quando clico no botão "Testar" do provider "openai"
    Então o sistema chama POST /api/admin/llm/providers/openai/test
    E um feedback visual de sucesso é exibido
    E a mensagem confirma que a conexão está funcional

  Cenário: Testar conexão com falha
    Dado que o provider "gemini" possui API Key inválida
    Quando clico no botão "Testar" do provider "gemini"
    Então o sistema chama POST /api/admin/llm/providers/gemini/test
    E um feedback visual de erro é exibido
    E uma mensagem de erro detalhada é mostrada

  Cenário: Testar provider desabilitado
    Dado que o provider "claude" está desabilitado
    Quando faço POST /api/admin/llm/providers/claude/test
    Então o resultado indica que o provider está desabilitado
