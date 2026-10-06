# ADR-035 — Isolamento e confiabilidade do núcleo

Data: 2026-09-28 · Issues: #111–#117 · Stories: BACK-FIX-111–117 · [Plano/status](../../plan/backend-core-remediation.md).

Decisão: aceita · Implementação: aplicada na branch `fix/backend-core-tenancy` · Validação dos cenários centrais: aprovada; gate global de cobertura do CI permanece pendente conforme o [relatório](../../backend/validation/backend-core-remediation.md).

## Contexto

A auditoria reproduziu elevação de API keys, seleção de tenant divergente em hubs, filtro RAG aberto com lista vazia/SQL inválido, isolamento MAF sem chave, concorrência de quotas e colisões no seeding de skills. O usuário aprovou o alvo da ADR-034: administração de tenants/planos separada de acesso temporário auditado a conteúdo; a ACL de sala continua obrigatória.

## Decisão

- Tenant comum precisa existir, estar ativo e ter membership. Header/query/claim seguem a mesma validação; role tenant não atravessa tenants. API key recebe o papel persistido e papel desconhecido falha fechado.
- Platform Admin fica em tabela própria. Owner/Admin de tenant não são promovidos automaticamente. Bootstrap usa IDs explicitamente configurados e não reintroduz administradores removidos depois do bootstrap.
- Administrações globais — tenants, planos e registry Gateway — exigem registro explícito em `platform_administrators`. Gateway REST/hub também protege dashboards/status; nomes de serviço inexistentes retornam 404 sem broadcast. Eventos operacionais são endereçados ao grupo do tenant selecionado.
- Suporte a conteúdo é temporário: usuário já membro, uma sala, role Reader, motivo, validade de até sete dias, revogação e auditoria de grant/acesso. ACL de sala permanece necessária; Platform Admin não recebe acesso implícito.
- RAG filtra tenant e salas permitidas na query SQL antes do ranking. Lista de salas vazia retorna zero resultados. Ingestão em sala exige escrita e persiste room/document/source byte metadata.
- MAF recebe chave de isolamento tenant/usuário. Retomada REST e eventos de sessão verificam owner/tenant; estado serializado e mensagens permanecem persistidos após restart.
- `Tenant.Limits` é a fonte de teto para RPM, tokens/custo diários, sessões, agentes, documentos lógicos e bytes de origem. Configuração de quota pode restringir teto do plano. Chamadas via runtime `IChatClient` consultam quota estimada antes do provider e gravam uso de tokens/custo após sucesso.
- Uso diário e reset UTC ficam no PostgreSQL com upserts atômicos. A janela RPM do limiter é local ao processo. O teste de concorrência cobre factories/repos independentes, mas não dois hosts da API.
- Skills padrão têm IDs tenant-scoped; catálogo parcial é completado sem perder IDs/customizações e permanece estável após restart.

## Consequências

Memberships legadas preservam tenant e papel; migration não cria Platform Admin a partir de roles antigos. Grants temporários são ligados à ACL pelo mesmo ID para negar leitura depois de expiração/revogação. Quotas de chat persistem e bloqueiam token/custo excedidos também após restart. Preflight usa estimativa conservadora; métricas de provider são preferidas para o uso final.

Bytes de armazenamento são contabilidade lógica do conteúdo de origem associado ao documento vetorial; não incluem espaço físico de índices, overhead PostgreSQL ou cópias de arquivo. `MaxMonthlyBudgetUsd` legado é projeção de custo diário × 30, não contador mensal. `ServiceGateway` ainda não registra providers no startup de produção; o cenário do broadcaster usa um serviço fixture restrito ao ambiente `Validation`. `TokenAuditService` é best-effort; `tenant_quotas` é a fonte de enforcement.

## Validação

Solução e harness Release compilados com zero avisos/erros. Suíte: 701 aprovados, 1 ignorado, 0 falhas. PostgreSQL 16.15/pgvector 0.8.6/Ollama reais: integração core 43/43; Gateway `ServiceStatusChanged` e isolamento por tenant aprovados; store/skills/quota 10/10; migration/backfill legado aprovado; sessão MAF, skills A/B e quotas tokens/custo após restart aprovados. REST, OpenAI-compatible e SSE demonstraram negação de quota antes de novo despacho; API key não consegue trocar tenant no handshake SignalR e tenant inativo é rejeitado em HTTP/hub. Cobertura global não foi reavaliada após o follow-up; última medição disponível foi 21,08%, abaixo do gate CI de 80%. Não inferir estabilidade de módulos fora desses cenários.
