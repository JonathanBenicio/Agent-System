# ADR-035 — Isolamento e confiabilidade do núcleo
Data: 2026-09-28 · Issues: #111–#117 · Stories: BACK-FIX-111–117 · [Plano](../../plan/backend-core-remediation.md)
Decisão: aceita · Implementação: aplicada nesta branch · Validação integrada: aprovada nos cenários executados, com lacunas descritas no [relatório](../../backend/validation/backend-core-remediation.md)

## Contexto
A auditoria reproduziu elevação de API keys, seleção de tenant divergente nos hubs, RAG aberto com lista vazia/SQL inválido, isolamento MAF sem chave, concorrência de quotas e colisões no seeding de skills. O usuário aprovou o alvo da ADR-034: administração de tenants/planos separada de acesso temporário auditado ao conteúdo; ACL de sala continua obrigatória.

## Decisão
- Tenant comum precisa existir, estar ativo e ter membership autenticada. Header/query/claim seguem a mesma regra. Admin de tenant não atravessa tenants. API key usa role persistida; role inválida falha fechada.
- Platform Admin fica em tabela própria, sem promoção de Owner/Admin legados. Bootstrap usa somente IDs explícitos de configuração e não substitui uma lista já existente.
- Rotas `/api/platform/tenants` listam tenants e atualizam planos. Alteração de plano preserva `Tenant.Limits`, que pode impor teto menor.
- Suporte a conteúdo é temporário e requer usuário já membro do tenant, sala definida, role Reader, motivo, validade máxima de sete dias, revogação e auditoria de grant e acesso. ACL por sala permanece necessária; Platform Admin não tem acesso implícito.
- RAG aplica tenant e sala na consulta SQL antes do ranking; lista vazia retorna zero sem embedding.
- O hosting MAF recebe chave de isolamento derivada de tenant/usuário. Retomada de sessão verifica ownership e tenant.
- Uso diário de quota e reset são statements PostgreSQL atômicos. Plano é teto e quota configurada pode restringir; leitura não autoriza por snapshot diário em cache.
- Defaults de skill recebem ID tenant-específico; catálogo parcial é completado, customizações e IDs antigos são preservados, falhas de banco não viram catálogo vazio.

## Consequências
O modelo aditivo de memberships/grants mantém papéis existentes e não cria administradores de plataforma automaticamente. Grants temporários são ligados à ACL pelo ID, permitindo recusa após expiração/revogação sem bypass do store de salas. Quotas concorrentes são contabilizadas no banco. Limites de sessões/agentes/storage ainda têm modelos diferentes e permanecem explicitamente fora da unificação atual.

## Validação
API Release compilou sem avisos; suíte unitária: 689 aprovados, 1 ignorado, 0 falhas. PostgreSQL 16/pgvector/Ollama reais: 29 cenários do diagnóstico HTTP/SignalR e 9 cenários do diagnóstico store/skills/quota aprovados. Foram validados memberships e negações, grants auditados/expirados/revogados, 61 candidatos RAG, quota concorrente e reset, limites de plano, REST/SSE/SignalR chat e persistência de registros após restart. Restore do estado interno MAF, entrega cruzada negativa em todos os cinco hubs, backfill contra fixture legado e enforcement uniforme de recursos seguem como lacunas. Não inferir estabilidade de módulos não exercitados.