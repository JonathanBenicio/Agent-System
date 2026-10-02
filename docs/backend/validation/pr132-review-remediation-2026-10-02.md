# Validação — Correções da revisão do PR #132
Data de abertura: 2026-10-02 · [Epic #139](https://github.com/JonathanBenicio/Agent-System/issues/139) · [Plano/matriz32](../../plan/pr132-review-remediation.md) · [ADR041](../../architecture/adr/041-pr132-review-remediation.md)
Baseline: e8e813b1887e6e23f4cdf61031392f27e2a32334 · Branch:`codex/pr132-review-remediation`
Status: em execução; gate integrado da correção ainda não executado.

## Evidência por contexto
| Contexto | Issue | SHA | Verificação/ambiente | Resultado |
|---|---|---|---|---|
| Governança R30, contribuição solicitada no outro chat | #150 |169c3f8|diff de AGENTS/GEMINI/workflow/commit-rules e git diff --check|passou; não conclui R29|
| auth | #140 | pendente | Authorization cru sem Bearer aplica tenant ativo, membership e papel; associação revogada/inativa é negada antes de executar. Login usa cookie HttpOnly sem credencial em localStorage/headers persistidos; logout chama o backend e invalida autenticação cookie. Preview de skill usa renderização Markdown segura; payload HTML/script/event handler não executa nem cria conteúdo ativo. Regressões de login/logout/raw-key revogada e stored XSS exercitam os caminhos reais. | não executado |
| fides | #141 | pendente | Chamada direta e roteamento simples inspecionam conteúdo antes do provider com a política do tenant. Credenciais obrigatórias são redigidas; falha/timeout de proteção bloqueia despacho. Não adicionar caminho paralelo nem promover Lab; testes com provider fake capturam o conteúdo realmente enviado. | não executado |
| onnx | #142 | pendente | Upload/update/test e worker validam dimensões, canais e cálculo de bytes com aritmética segura antes de alocar. Limites configuráveis possuem padrão seguro e erro de validação claro; configurações/modelos válidos seguem funcionais. Regressões rejeitam negativos, overflow e tamanhos acima do orçamento sem provocar OOM. | não executado |
| tenancy-rag | #143 | pendente | Ingestão de sala exige Editor/Admin, incluindo suporte; Reader é negado antes de gravar. Repositório dinâmico InMemory particiona por tenant; nomes iguais não colidem nem vazam/desativam outros tenants. Filtro room_ids tem semântica compartilhada de lista permitida em InMemory/SQLite/Pinecone e PostgreSQL; vazio nega. Drop/upload envia roomId distinto de source e chunks aparecem apenas na sala/tenant autorizados. | não executado |
| quotas | #144 | pendente | Quota InMemory faz reset diário determinístico, preservando isolamento; PostgreSQL mantém uso após restart. Stream cancelado/falho registra consumo recebido e estimativa explícita quando provider não reporta uso, sem duplicação. Contagem de sessões consulta todas as ativas; histórico encerrado recente não esconde ativas antigas. Criação aplica teto em REST/SSE/SignalR/direct; continuação da sessão autorizada funciona no teto; concorrência não permite ultrapassar limite. | não executado |
| workflows | #145 | pendente | Tipos de step são estáveis e JSON/UI compartilham string enum; round-trip de todos os tipos preserva significado. Aprovação/rejeição identifica step; duas aprovações paralelas podem ser decididas independentemente, com compatibilidade para uma pendente. Outputs paralelos têm merge/sincronização segura; ciclos e dependências ausentes são rejeitados antes de salvar/executar. Claim global requer SystemOperationContext interno; execução tenant-owned usa tenant real. UI só confirma decisão após sucesso e preserva estado/mostra erro em 403/409/500/offline. | não executado |
| analytics | #146 | pendente | Scope/DbContext permanecem vivos até finalizar consultas de custos/performance/sessões/workflows. Teste com provider DI real de escopo detecta descarte prematuro; tenant não pode consultar dados de outro. Cancelamento e erro encerram scope corretamente sem vazamentos. | não executado |
| memory | #147 | pendente | Perguntas distintas do mesmo usuário/tenant não reutilizam resposta específica da primeira consulta. Cache inclui consulta normalizada/filtros pertinentes ou evita cache de contexto específico; isolamento usuário/tenant preservado. Regressões cobrem resultado vazio seguido de matches e perguntas não relacionadas. | não executado |
| agent-contracts | #148 | pendente | UI/SDK/API concordam em /api/agent/tools list/get/delete/execute e auth/tenant permanecem obrigatórios. Template e serializer visual geram metadata/instructions válidos para o DTO, com rota save-yaml real. Alternar visual/YAML e salvar/reabrir preserva campos; testes validam manifesto oficial e rotas. | não executado |
| ci | #149 | pendente | Workflow roda para develop e branch integration de #132; job Playwright inicia frontend e aguarda servidor. Mocks usam /api/chat/configuration e content/agentName; teste XSS confirma renderização antes da ausência de execução. Teste timeout usa política atual/controlável e verifica recuperação; Cypress inicia/usa servidor frontend correto. Scripts SQL recebem nome de projeto Compose da execução, nunca consultam stack/banco não relacionado. Lint/build/Playwright/Cypress e diagnóstico de isolamento passam; falhas não são convertidas em sucesso. | não executado |
| docs | #150 | pendente | Cada story tem ID único e todas as referências ADR/plano/índices apontam ao significado correto. GEMINI/AGENTS/templates concordam Refs para parcial e Closes apenas critérios completos intencionais. Planos/ADRs/docs normativos refletem contratos corrigidos e evidência real; histórico não é apresentado como validação atual. Estado das issues antigas é confrontado com critérios completos; não fechar por subconjunto. | não executado |
| durabletask | #151 | pendente | PostgreSQL 16 executa dt.complete_tasks para lote0/1/2+ sem DISTINCT+FOR UPDATE inválido e sem ROW_COUNT agregado incorreto. Probe transacional valida deleção/conclusão atômica e concorrência/retry conforme contrato; erro não publica resultado parcial. Instalação limpa aplica migrations e cria tabelas/modelo corretos; engine padrão permanece dinâmico e DurableTask opcional. Se correção exigir nova migration, manter cadeia explícita e has-pending-model-changes limpo. | não executado |

## Gate final

### Contexto #140 — autenticação e preview
- Backend: `dotnet test ... --configuration Release --filter 'FullyQualifiedName~CookieAndOpenAiAuthorizationTests|FullyQualifiedName~ApiKeyAuthenticationTests|FullyQualifiedName~TenantMiddlewareTests'`: **23 aprovados,0 falhas/ignorados**. TestServer usa os controllers/handler/middleware reais e PostgresPermissionService com EF InMemory; orquestrador/quota são doubles. Revogação, inatividade, spoofing, vínculo em outro tenant, papel Viewer e cookie foram exercitados.
- Frontend: `npm run lint` e `npm run build`: passaram após `npm ci` no lockfile, sem mudar dependências/versões.
- `auth-cookie-skills.e2e.spec.ts`, Chromium: **4 aprovados**. UI React real, API mockada; asserts provam cookie HttpOnly/sem chave JS, restore/logout/erro e renderização Markdown antes da negação de XSS.
- Ambiente local: Vite5193; Playwright1.60 em WSL Ubuntu26 usa runtime Ubuntu24 via override documentado pelo pacote e bibliotecas oficiais. Isso não afirma suporte oficial ao Ubuntu26 nem valida backend/provider produção.
- Falhas iniciais de fixture (IQuotaEnforcer ausente e glob interceptando módulos JS) corrigidas; os resultados finais acima foram reexecutados. Gate integrado PostgreSQL/CI ainda pendente.

| Verificação | SHA/ambiente/comando | Resultado/evidência |
|---|---|---|
| Backend Release/regressões/suíte | pendente | não executado |
| Frontend lint/build/E2E Playwright/Cypress | pendente | não executado |
| Banco vazio migrations/schema/modelo | PostgreSQL 16 Compose55432; DB exclusivo a registrar | não executado nesta correção |
| dt.complete_tasks0/1/2+ e atomicidade | DB exclusivo, probes com rollback | não executado nesta correção |
| Links locais/índices | pendente | não executado |
| Diff/staging/commits/PR/base/remote | pendente | não executado |

## Limites da evidência
A revisão anterior contabilizou732 paths, leu código/testes por diff/contexto e classificou históricos/gerados por origem/refs; isso não é evidência de todos os cenários em runtime. Os18 migrations/69 tabelas da baseline foram verificados antes destas correções; repetir no gate da entrega. Não reutilizar esses números como prova desta branch.
Provider fake, skips, testes unitários, SQL real e UI/E2E serão separados. Sem prova de provider produção, protocolos preview ou multihost de supervisor nesta entrega. Cobertura medida será registrada, sem inferir cumprimento de 80%.

## Segurança operacional
Nunca conectar 5432 ou outro banco. Registrar Compose/porta/db/container resolvidos antes de qualquer teste. Criar e excluir apenas DB temporário próprio; restaurar serviço parado se foi iniciado pela validação; preservar volume/bancos existentes. Não publicar chaves, dados sensíveis ou logs brutos.

## Auditoria de conclusão
Antes de concluir a epic: cada R01–R32 precisa de commit e verificação específica que cubra o defeito original. Critérios de issues antigas são avaliados inteiros, sem fechar por subconjunto. Descrições/PR devem diferenciar decisão, implementação e validação.
