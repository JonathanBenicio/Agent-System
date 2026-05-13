# Editor de Agentes Declarativo com Suporte a YAML, Validação e Versionamento Histórico

Este plano propõe a implementação de um fluxo completo para criação e edição declarativa de agentes através de duas interfaces integradas (Formulário Visual Simples e Editor YAML Avançado), com validação em tempo real e controle de histórico de versões integrado ao banco de dados PostgreSQL do ecossistema.

---

## 🎯 Objetivo
Habilitar o conceito de **"Agent-as-Code"**, permitindo que usuários gerenciem as configurações completas de seus agentes via formulários visuais ou blocos YAML declarativos, com validações inteligentes e capacidade de rollback para versões históricas salvas no banco de dados.

---

## 🛠️ Detalhamento das 3 Fases de Implementação

### FASE 1: API de Validação e Parser de YAML (Backend)
Foco em receber o YAML de entrada, validá-lo contra erros sintáticos (formato YAML) e semânticos (regras de negócio do `AgentSpecification`), e retornar feedbacks detalhados de erros para exibição no editor.

*   [ ] **Tarefa 1.1:** Instalar o pacote `YamlDotNet` no projeto [AgenticSystem.Infrastructure](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Infrastructure/AgenticSystem.Infrastructure.csproj) para garantir parsing completo de block scalars e validação sintática.
    *   *Verificação:* Executar `dotnet build` e verificar sucesso.
*   [ ] **Tarefa 1.2:** Desenvolver o validador `AgentYamlValidator` na camada de `Core` ou `Infrastructure` que tenta desserializar o YAML para `AgentSpecification` e valida se os campos obrigatórios (`Name`, `Instructions`, `Description`, `Tier`) existem e se as ferramentas listadas no `AllowedTools` realmente existem no catálogo do sistema.
    *   *Verificação:* Criar teste de unidade validando cenários com YAMLs válidos e inválidos.
*   [ ] **Tarefa 1.3:** Criar o endpoint `POST /api/agents/validate` no [AgenticSystem.Api](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Api/) que recebe uma string contendo o YAML e retorna um objeto detalhando se é válido ou uma lista de erros (com linha, coluna e mensagem).
    *   *Verificação:* Chamada `POST` via Postman/curl retorna `200 OK` com `isValid: true` para um YAML válido.

---

### FASE 2: UI do Formulário Simples ↔ Avançado YAML (Frontend)
Foco na experiência do usuário, oferecendo edição híbrida onde o Formulário Visual (fácil de usar) e o Editor YAML (poderoso) editam o mesmo modelo de dados de forma síncrona.

*   [ ] **Tarefa 2.1:** Configurar o editor de código avançado (Monaco Editor ou CodeMirror 6) na aba "Avançado" da página de Edição de Agente no Frontend, integrando realce de sintaxe YAML.
    *   *Verificação:* Abrir a aba "Avançado" e visualizar o editor com identação e numeração de linhas.
*   [ ] **Tarefa 2.2:** Desenvolver o mecanismo de sincronização bidirecional em tempo real:
    1. Alterações no Formulário Visual geram e atualizam o YAML no editor.
    2. Alterações válidas no YAML (com debounce) preenchem e atualizam os inputs do Formulário Visual.
    *   *Verificação:* Digitar o nome no formulário visual e ver o campo `metadata.name` ser atualizado automaticamente no YAML.
*   [ ] **Tarefa 2.3:** Integrar os componentes ricos no Formulário Simples:
    *   *AllowedTools:* Componente Combobox Badge Multi-Select categorizando Skills C#, ferramentas MCP e agentes de handoff.
    *   *AutonomyLevel:* Segmented step-control com cores graduais (de verde a vermelho) refletindo níveis L0 a L5.
    *   *Configuration.Model:* Dropdown customizado com tags visuais (`⚡ Rápido`, `🧠 Raciocínio`).
    *   *Verificação:* Interagir com os seletores e confirmar o binding correto no modelo.

---

### FASE 3: Histórico e Versionamento integrado com Banco (Full-stack)
Aproveitar a estrutura robusta já existente de `IAgentVersioningService` no backend para expor o histórico de snapshots e rollbacks na interface visual.

*   [ ] **Tarefa 3.1:** Criar os endpoints HTTP para o serviço de versionamento no controlador de agentes na API:
    *   `POST /api/agents/{name}/save-yaml` (cria uma nova versão de configuração de agente via YAML e salva no banco usando `IAgentVersioningService.CreateVersionAsync`).
    *   `GET /api/agents/{name}/history` (lista todas as versões históricas do agente via `GetVersionHistoryAsync`).
    *   `POST /api/agents/{name}/rollback/{versionId}` (reverte o agente ativo para uma versão anterior usando `RollbackAsync`).
    *   *Verificação:* Endpoints expostos na documentação do Swagger e operando corretamente integrados com `PostgresAgentVersionStore`.
*   [ ] **Tarefa 3.2:** Criar a aba "Histórico de Versões" na UI do agente, exibindo uma timeline vertical com as versões (`v3`, `v2`, `v1`), quem alterou, quando e o comentário de alteração (changelog).
    *   *Verificação:* Renderizar a timeline após salvar 2 alterações consecutivas no agente.
*   [ ] **Tarefa 3.3:** Implementar o visualizador de diferenças (Diff-View) e o botão "Rollback" com um clique na tela de Histórico.
    *   *Verificação:* Clicar em reverter para a `v1` e verificar que o editor foi atualizado para as configurações originais e um novo snapshot foi gerado com sucesso.

---

## 🏁 Critérios de Aceitação (Done When)
- [ ] O backend compila perfeitamente sem erros e valida YAMLs sintática e semanticamente contra o esquema de agentes.
- [ ] O frontend oferece alternância fluida entre edição Visual (Simples) e declarativa (YAML Avançado) com sincronização bidirecional em tempo real.
- [ ] Cada alteração salva gera uma nova versão auditada na tabela `AgentVersions` do banco PostgreSQL.
- [ ] O usuário consegue visualizar diferenças de arquivos (Diff) no histórico e fazer Rollback com sucesso total.
- [ ] A suíte de 603 testes passa com 100% de sucesso.

---

## 🚀 Ferramentas de Verificação Recomendadas
- **Backend Quality Check:** `python .agents/skills/lint-and-validate/scripts/lint_runner.py`
- **Unit & Integration Tests:** `python .agents/skills/testing-patterns/scripts/test_runner.py`
- **Database Schema Validation:** `python .agents/skills/database-design/scripts/schema_validator.py`
- **UX Audit & Access:** `python .agents/skills/frontend-design/scripts/ux_audit.py`
