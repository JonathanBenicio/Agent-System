# Roadmap: Alinhamento de User Stories & Associação de Agentes a Knowledge Rooms (US-41)

> **Status documental:** Em Execução
> **Escopo:** Saneamento documental em USER-STORIES.md e implementação completa de ponta a ponta da US-41 (associação de agentes a salas de conhecimento)
> **Fonte de verdade operacional:** [ADR-019](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/019-agent-room-association.md)
> **Gerado em:** 2026-05-21
> **Projeto:** AgenticSystem

---

## User Review Required

Documentamos abaixo decisões de design e de comportamento que influenciarão diretamente a implementação e a arquitetura visual/lógica final do sistema.

> [!IMPORTANT]
> **Purple Ban Ativo:** O novo seletor de salas e seus componentes visuais no modal utilizarão estritamente cores da paleta base de cinza (`zinc-800`, `zinc-900`) e detalhes em teal (`teal-500`, `teal-600`), respeitando a restrição absoluta contra qualquer tom de violeta/roxo.

---

## Open Questions

> [!WARNING]
> Solicitamos a análise e resposta do usuário para as seguintes questões estratégicas de implementação antes de iniciarmos a codificação dos componentes:

1. **Fallback de Contexto Vazio vs Global:** Se o administrador salvar um agente sem nenhuma sala de conhecimento selecionada (lista vazia), o agente deve:
   - **Opção A (Recomendada):** Ter acesso a *todas* as salas ativas do tenant atual (mantendo o comportamento herdado atual de escopo global).
   - **Opção B:** Ter acesso a *nenhuma* sala (contexto RAG totalmente vazio para segurança máxima por padrão).
2. **Posicionamento do Seletor de Salas no Modal:** Para otimizar o fluxo visual e o espaço vertical no `AgentFormModal.tsx`, propomos colocar o seletor de salas logo abaixo da seção "Capacidades Operacionais (Capabilities)", formatado como chips removíveis zinc/teal de alta qualidade que abrem um menu flutuante. Você concorda com essa disposição ou prefere em outro bloco?
3. **Associação ao salvar via YAML Declarativo:** Como as salas de conhecimento são entidades puramente de infraestrutura e persistência relacional do banco de dados (não fazem parte da especificação do agente no Microsoft Agent Framework), elas não aparecem no schema padrão de YAML. Propomos que a seleção de salas feita na aba "Formulário Visual" seja mantida e salva em background pelo frontend mesmo se o usuário salvar o agente através da aba "YAML Declarativo". Confirma que este é o comportamento esperado?

---

## Objetivo

O objetivo desta iniciativa é duplo:
1. **Saneamento Documental Extensivo:** Sincronizar o [USER-STORIES.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/USER-STORIES.md) com a realidade do repositório, marcando como `✅ Implementado` as capacidades prontas (Reranker local ONNX, Canvas de Workflows, Webhooks, Alertas de Consumo e Dynamic ONNX Engine) e resolvendo a duplicidade entre `ML35` e `ML40`.
2. **Implementar a US-41 (Associação de Agente a Knowledge Rooms):** Concluir a integração pendente para permitir restringir o escopo semântico dos agentes em salas de conhecimento por meio de uma interface robusta.

## Princípios de Implantação

1. **Purple Ban (Tailwind CSS):** Proibido o uso de qualquer tom de violeta ou roxo nos novos componentes visuais. Seguir estritamente a paleta padrão baseada em cinza/zinc e teal.
2. **Isolamento de Tenants (Multi-Tenancy):** Todo acesso e modificação das associações entre agentes e salas de conhecimento deve obedecer estritamente ao cabeçalho `X-Tenant-Id` obtido através do helper do controller.
3. **Resiliência e Retrocompatibilidade:** Se um agente não possuir nenhuma sala de conhecimento explicitamente associada, o comportamento padrão deve ser mantido conforme a Opção escolhida nas Open Questions.
4. **Sem Placeholders:** Nenhum código temporário ou mocks. Integração direta com a base de dados real via Entity Framework Core no backend.

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | Saneamento Documental | Ajustar a base de verdade do projeto ([USER-STORIES.md](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/USER-STORIES.md)) removendo redundâncias e atualizando status corretos. |
| 2 | Backend: APIs de Associação | Expor endpoints de GET e PUT para gerenciar a associação no `AgentController.cs` injetando `IAgentKnowledgeRoomStore`. |
| 3 | Frontend: Integração do Multiselect | Atualizar o `AgentFormModal.tsx` para listar as salas disponíveis dinamicamente, permitindo a seleção múltipla e persistência no submit do formulário. |
| 4 | Verificação e Validação | Validar testes de build, executar testes unitários do backend e fazer auditorias de lint/UX. |

---

## Detalhamento: US-41 — Associação de Agente a Knowledge Rooms

### Por que implementar?
Atualmente, a junction table `AgentKnowledgeRoomAssignmentEntity` e o store `PostgresAgentKnowledgeRoomStore.cs` estão prontos, mas não expõem pontos de entrada via API ou interface de usuário. Isso impede que os administradores restrinjam o conhecimento de um agente, permitindo vazamento potencial de escopo ou acessos indesejados a dados não pertinentes.

### Componentes propostos

| Componente | Papel |
|---|---|
| `AgentController` | Expõe `GET /api/agent/agents/{name}/rooms` e `PUT /api/agent/agents/{name}/rooms` integrados ao `IAgentKnowledgeRoomStore`. |
| `agentApi` (Frontend) | Registra os métodos `getRooms` e `setRooms` para realizar chamadas REST assíncronas para os novos endpoints. |
| `AgentFormModal.tsx` | Renderiza a seção de "Salas de Conhecimento Autorizadas" utilizando o componente customizado de seleção múltipla com chips interativos (seguindo estética premium). |

### Plano por etapas

#### Etapa 1: Saneamento de USER-STORIES.md
1. **Remover a Duplicata ML40:** Excluir todo o bloco `ML40 — Smart Triage & Fast Path` (linhas 1239 a 1259) que é idêntico a `ML35`.
2. **Atualizar Tabela Geral:** Atualizar a linha 1278 para refletir `ML35–ML39` (com contagem `3` e status `⏳`).
3. **Atualizar Status Individuais:**
   - Mudar `ML38 — RAG Avançado (Reranker)` para `✅ Implementado` e marcar critérios de aceite.
   - Mudar `US-34` e `US-35` (Workflows Canvas) para `✅ Implementado` e marcar critérios de aceite.
   - Mudar `US-36` e `US-37` (Webhooks CRUD) para `✅ Implementado` e marcar critérios de aceite.
   - Mudar `US-38` e `US-39` (Alertas de Consumo) para `✅ Implementado` e marcar critérios de aceite.
   - Mudar `US-45`, `US-46` e `US-47` (Dynamic ONNX Inference Engine) para `✅ Implementado` e marcar critérios de aceite.
   - Atualizar a `US-41` (modal de associação) para `🚧 Em Progresso` e marcar o critério de persistência no banco.

#### Etapa 2: Implementação do Backend (US-41)
1. **Injetar Dependência em AgentController.cs:** Adicionar `IAgentKnowledgeRoomStore? agentRoomStore = null` no construtor do controller e guardá-lo no campo `_agentRoomStore`.
2. **Adicionar Endpoints:**
   - `GET /api/agent/agents/{name}/rooms`: Obtém lista de IDs das salas atribuídas.
   - `PUT /api/agent/agents/{name}/rooms`: Define salas atribuídas para o agente chamando `SetRoomsForAgentAsync`.
3. **Helper GetTenantId():** Implementar `private string GetTenantId() => Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";`.

#### Etapa 3: Implementação do Frontend (US-41)
1. **Atualizar a API Client (`frontend/src/lib/api.ts`):**
   - Adicionar `getRooms: (name: string) => get<string[]>('/api/agent/agents/' + encodeURIComponent(name) + '/rooms')`
   - Adicionar `setRooms: (name: string, roomIds: string[]) => put<void>('/api/agent/agents/' + encodeURIComponent(name) + '/rooms', roomIds)`
2. **Integrar Seletor de Salas no `AgentFormModal.tsx`:**
   - Carregar salas ativas via `knowledgeRoomApi.list()`.
   - Carregar as salas associadas atuais do agente se for modo edição usando `agentApi.getRooms(agent.name)`.
   - Criar uma nova seção visual chamada "Salas de Conhecimento Autorizadas (Knowledge Rooms)" usando chips interativos (estética premium, sem violeta, com bordas zinc-800 e fundo zinc-900/50).
   - Ao salvar o formulário: se for YAML, salvar apenas a spec no backend; se for visual, salvar a spec e disparar a atualização de salas via `agentApi.setRooms(name, selectedRoomIds)` sequencialmente.

---

## Critérios de Aceite e SLOs
* [ ] Mudanças em `docs/USER-STORIES.md` aplicadas com sucesso e validadas sintaticamente.
* [ ] Backend expõe novos endpoints com controle estrito de Multi-Tenancy (`X-Tenant-Id`).
* [ ] Modal de agente no frontend permite associar e desassociar salas com salvamento operacional real.
* [ ] Build do frontend passa com `npm run build` sem erros de tipagem.
* [ ] Testes unitários do backend são validados via `dotnet test` com 100% de sucesso.

## Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Erros de concorrência ou delay de gravação no banco ao salvar agente e salas sequencialmente | No frontend, executar em Promise.all ou aguardar a gravação da especificação do agente com sucesso antes de chamar a API de associação de salas de conhecimento, exibindo indicador "Gravando..." unificado. |
| Inconsistência no salvamento de agente via aba de YAML declarativo | A associação de salas é ortogonal à especificação YAML do agente (a spec do MAF gerencia comportamento de LLM e ferramentas, enquanto as salas operam na infraestrutura de segurança do banco). Caso o usuário salve via YAML, o frontend persistirá as salas de conhecimento selecionadas na aba visual separadamente. |
