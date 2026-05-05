# language: pt
Funcionalidade: Agent Management
  Como administrador do AgenticSystem
  Quero gerenciar o catálogo de agentes (listar, criar, editar, excluir)
  Para controlar a hierarquia de especialistas do sistema

  Contexto:
    Dado que estou autenticado como administrador

  # ──────────────────────────────────────────────
  # US-09 — Listar agentes com filtro por tier
  # ──────────────────────────────────────────────

  Cenário: Grid de agentes com busca por nome
    Dado que acesso a página "/agents"
    Quando os agentes são carregados via GET /api/agent/agents
    Então um grid de agents é exibido
    E cada card mostra nome, tier e domínio do agente

  Cenário: Filtrar por tier
    Dado que estou na página "/agents"
    E existem agentes nos tiers 0 (Chief), 1 (Master), 2 (Specialist), 3 (Support)
    Quando seleciono o filtro "Specialist" (tier 2)
    Então apenas agentes do tier 2 são exibidos
    E o contador de resultados reflete a filtragem

  Cenário: Badge colorido por tier
    Dado que estou na página "/agents"
    Então cada agente exibe badge colorido correspondente ao tier
    E os badges seguem a paleta: Chief, Master, Specialist, Support

  Cenário: Filtrar agentes por tier via API
    Quando faço GET /api/agent/agents/tier/2
    Então o status de resposta é 200
    E todos os agentes retornados possuem tier 2

  # ──────────────────────────────────────────────
  # US-10 — Criar novo agente
  # ──────────────────────────────────────────────

  Cenário: Criar agente via formulário
    Dado que estou na página "/agents"
    Quando clico no botão "Criar Agent"
    E preencho: nome "ComplianceExpert", tier "2", domínio "compliance", temperatura "0.7"
    E confirmo a criação
    Então o sistema chama POST /api/agent/agents com o body AgentSpecification
    E um toast de sucesso é exibido
    E o novo agente aparece na lista

  Cenário: Validação de campos obrigatórios na criação
    Dado que abri o modal de criação de agente
    Quando tento confirmar sem preencher o nome
    Então uma mensagem de validação indica que o nome é obrigatório
    E o formulário NÃO é submetido

  Cenário: Validação de temperatura válida
    Dado que abri o modal de criação de agente
    Quando preencho temperatura com "3.0" (acima do limite 2.0)
    Então uma mensagem de validação indica que a temperatura deve estar entre 0.0 e 2.0

  # ──────────────────────────────────────────────
  # US-11 — Editar agente existente
  # ──────────────────────────────────────────────

  Cenário: Editar agente via formulário pré-preenchido
    Dado que o agente "AnalysisAgent" existe
    Quando clico em "Editar" no card do "AnalysisAgent"
    Então o modal de edição abre com campos pré-preenchidos
    E o nome, tier, domínio e temperatura estão preenchidos com valores atuais

  Cenário: Salvar edição do agente
    Dado que estou editando o agente "AnalysisAgent"
    Quando altero o domínio para "data-analysis" e confirmo
    Então o sistema chama PUT /api/agent/agents/AnalysisAgent
    E um toast de sucesso é exibido
    E a lista é atualizada com os novos dados

  # ──────────────────────────────────────────────
  # US-12 — Excluir agente com confirmação
  # ──────────────────────────────────────────────

  Cenário: Excluir agente com modal de confirmação
    Dado que o agente "TestAgent" existe na lista
    Quando clico em "Excluir" no card do "TestAgent"
    Então um modal de confirmação (variante danger) é exibido
    E o nome "TestAgent" aparece na mensagem de confirmação

  Cenário: Confirmar exclusão
    Dado que o modal de confirmação de exclusão está aberto para "TestAgent"
    Quando clico em "Confirmar"
    Então o sistema chama DELETE /api/agent/agents/TestAgent
    E um toast de sucesso é exibido
    E o agente "TestAgent" não aparece mais na lista

  Cenário: Cancelar exclusão
    Dado que o modal de confirmação de exclusão está aberto
    Quando clico em "Cancelar" ou pressiono Esc
    Então o modal é fechado
    E o agente NÃO é excluído

  # ──────────────────────────────────────────────
  # US-13 — Ver detalhes do agente
  # ──────────────────────────────────────────────

  Cenário: Modal read-only com detalhes completos
    Dado que estou na página "/agents"
    Quando clico no card do agente "PersonalAgent"
    Então um modal read-only é exibido com:
      | Campo        | Valor esperado      |
      | Nome         | PersonalAgent       |
      | Tier         | 1 (Master)          |
      | Capabilities | lista de capabilities|
      | Tools        | tools associadas     |
      | Skills       | skills associadas    |
      | Temperatura  | valor configurado    |

  # ──────────────────────────────────────────────
  # US-14 — Buscar agentes por nome
  # ──────────────────────────────────────────────

  Cenário: Busca em tempo real por nome
    Dado que estou na página "/agents"
    E existem 10 agentes cadastrados
    Quando digito "Analysis" no campo de busca
    Então apenas agentes com "Analysis" no nome são exibidos
    E a busca é case-insensitive

  Cenário: Combinar busca com filtro de tier
    Dado que estou na página "/agents"
    Quando digito "Agent" no campo de busca
    E seleciono filtro tier "Master"
    Então apenas agentes Master com "Agent" no nome são exibidos

  # ──────────────────────────────────────────────
  # Agent API — Edge Cases
  # ──────────────────────────────────────────────

  Cenário: Listar todos os agentes registrados
    Quando faço GET /api/agent/agents/all
    Então o status de resposta é 200
    E a lista inclui agentes de todos os tiers

  Cenário: Buscar agente por nome inexistente
    Quando faço GET /api/agent/agents/NaoExiste
    Então o status de resposta é 404

  Cenário: Tools — Listar por categoria
    Quando faço GET /api/agent/tools?category=search
    Então o status de resposta é 200
    E as tools retornadas pertencem à categoria "search"

  Cenário: Tools — Executar tool
    Quando faço POST /api/agent/tools/calculator/execute com body:
      """json
      { "action": "calculate", "parameters": { "expression": "2+2" }, "userId": "user1" }
      """
    Então o status de resposta é 200
    E o resultado contém o output da execução

  Cenário: Skills — Listar com filtro por agent e domínio
    Quando faço GET /api/agent/skills?agent=AnalysisAgent&domain=data
    Então o status de resposta é 200
    E as skills retornadas correspondem ao filtro

  Cenário: Maintenance — Cleanup de agentes inativos
    Quando faço POST /api/agent/maintenance/cleanup
    Então o status de resposta é 200
    E agentes inativos são removidos do registro
