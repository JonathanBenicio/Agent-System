# Roadmap: Refinamento de Listagem de Sessões de Usuário (Busca Back, Grupos Cronológicos e Estilo com Curvas)

> **Status documental:** Em Execução
> **GitHub Issue:** [#95](https://github.com/JonathanBenicio/Agent-System/issues/95)
> **Escopo:** Refinamento da barra lateral de chat, incluindo busca por texto no backend, agrupamento cronológico de conversas e transição da interface padrão para estilo com curvas arredondadas.
> **Fonte de verdade operacional:** [SessionController.cs](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/src/AgenticSystem.Api/Controllers/SessionController.cs), [SessionSidebar.tsx](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/frontend/src/components/chat/SessionSidebar.tsx)
> **Gerado em:** 23/05/2026
> **Projeto:** AgenticSystem

---

## Objetivo

Refinar a listagem de sessões criadas por usuário, garantindo uma interface extremamente premium, minimalista e interativa. As principais metas são:
1. **Busca no Backend**: Implementar a busca textual indexada pelo banco de dados por título e resumo executivo das conversas.
2. **Organização Cronológica**: Separar as sessões em categorias relativas de data ("Hoje", "Ontem", "Últimos 7 dias", "Mais antigas").
3. **Consolidação das Curvas**: Atualizar a folha de estilo e regras globais dos agentes em `.agents/` para estabelecer bordas arredondadas e curvas como a identidade visual oficial por padrão no projeto.

---

## Princípios de Implantação

1. **Retrocompatibilidade**: O endpoint de listar sessões (`/api/session`) continuará funcionando normalmente sem filtros caso o parâmetro `search` não seja enviado.
2. **Uso Eficiente de Recursos**: A busca no frontend é controlada por um debounce de 300ms, minimizando requisições redundantes ao servidor ASP.NET Core.
3. **Harmonia Visual (Sem Purple)**: Manter a paleta de cores escura e sofisticada, focada em tons de `zinc` e `teal`, evitando qualquer tom roxo.

---

## Fases e Sequenciamento

| Ordem | Frente/Fase | Motivo do sequenciamento |
|---|---|---|
| 1 | **Definição de Regras (.agents)** | Ajustar as regras dos agentes para garantir consistência no padrão visual de curvas. |
| 2 | **Backend API & Persistence** | Estender `ISessionStore` e `SessionController` para receber e executar o filtro de pesquisa em PostgreSQL e memória. |
| 3 | **Componentes & Custom Hooks** | Modificar a requisição e a barra lateral `SessionSidebar` para suportar agrupamento, busca e design curvo. |

---

## Detalhamento

### Componentes propostos

| Componente | Papel |
|---|---|
| `ISessionStore` | Assinatura atualizada contendo o parâmetro opcional `search`. |
| `PostgresSessionStore` | Busca otimizada no banco utilizando `EF.Functions.ILike` no JSON e tabela de resumos. |
| `SessionController` | Endpoint HTTP que repassa a consulta `search` para a persistência. |
| `SessionSidebar` | Painel de controle no frontend reestruturado com input arredondado, agrupamentos de data e tooltips discretos. |

### Plano por etapas

1. **Fase 1: Alinhamento de Padrões (.agents)**
   - Modificar `.agents/rules/front.md` e `.agents/agents/frontend-specialist.md` documentando que o padrão do projeto utiliza curvas (`rounded-xl` e `rounded-2xl`).

2. **Fase 2: Persistência & Controller (C#)**
   - Alterar `ISessionStore` nos projetos Core e Infrastructure.
   - Atualizar a implementação do Postgres para filtrar o `DataJson` e tabela `SessionSummaries`.
   - Adicionar o parâmetro no `SessionController.GetSessions`.

3. **Fase 3: Refinamentos na UI (React)**
   - Adicionar `search` nas funções do `api.ts` e hook `useSessions.ts`.
   - Modificar `SessionSidebar.tsx` adicionando agrupamento de datas, filtro de pesquisa com debounce, itens com bordas curvas (`rounded-xl`), e o sumário + contagem de mensagens exibidos suavemente em hover de metadados.

### Critérios de Aceite e SLOs
* [ ] Pesquisar no input filtra as sessões combinando tanto pelo título quanto pelo resumo.
* [ ] Sessões aparecem elegantemente distribuídas em seções cronológicas com divisores visuais bem desenhados.
* [ ] As bordas laterais e botões possuem formato arredondado refinado (`rounded-xl`), mantendo a harmonia visual.
* [ ] Nenhuma regressão é gerada nos testes existentes.

### Riscos e Mitigações

| Risco | Mitigação |
|---|---|
| Queda de desempenho em bancos PostgreSQL muito grandes ao buscar texto livre em `DataJson`. | O filtro é indexado e limitado pelo parâmetro `limit` (padrão 50 itens) na busca por ID de usuário ativo, mantendo a carga irrisória. |
