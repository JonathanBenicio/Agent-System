# Validação — Escopo de sistema sem tenant sintético

SHA base: `7d12bfbe275ff9e629801d6f203acdf4f269aee1` • Código testado: `d6821ae` • Issue/plano: #97 / `tenant-system-scope-remediation.md` • Branch: `integration/develop-pr-stack-2026-09-30` • Ambiente: Windows .NET 10 + PostgreSQL 16/pgvector no Compose de validação, isolado em `127.0.0.1:55432`.

## Resultados

| Cenário/critério | Verificação | Resultado | Evidência/gap |
|---|---|---|---|
| Build Release da solução | `dotnet build --no-restore --configuration Release` | Passou; 0 warnings, 0 errors | Inclui Core, Infrastructure, API e testes. |
| Harness de validação de backend | `dotnet build tests/backend-validation/BackendDiagnostics.csproj --no-restore --configuration Release` | Passou; 0 warnings, 0 errors | Usa somente as portas/DB do Compose de validação. |
| Suíte com PostgreSQL Compose | `dotnet test --no-build --configuration Release` com `AGENTIC_TEST_POSTGRES` e `AGENTIC_EF_CONNECTION` apontando para o Compose | 811 passaram, 5 ignorados, 0 falhas, total 816 | Ignorados: OCR de imagem/PDF, RAG PostgreSQL e integrações que exigem Ollama. Os 811 restantes passaram com PostgreSQL disponível. |
| Alertas de quota A/B | `TenantSystemAlertIsolationPostgresTests` | Passou | Cada tenant lê somente o alerta persistido sob seu tenant; a rota global usa outra tabela e autorização de plataforma. |
| Outbox por tenant | `SystemBackgroundTenantIsolationPostgresTests.OutboxProcessor_DispatchesAndMarksItUnderItsSourceTenant` | Passou | Mensagens foram publicadas e marcadas sob os tenants reais de origem. |
| Rotação de segredos | `SystemBackgroundTenantIsolationPostgresTests.SecretRotation_EnumeratesTenantsAndAuditsOnlyTheirExpiredKeys` | Passou | O job encontrou e auditou as chaves de A/B sob seus contextos; nenhum valor secreto foi escrito na descrição. |
| Migração de recursos antigos | `SeparatePlatformOwnedResources` aplicada no banco Compose após semear registros de validação com `platform`/`system-background` | Passou | Tool, skill e quota migraram para tabelas platform; pseudo-registros restantes = 0; evento com tenant real voltou ao outbox correto; evento global foi para o outbox platform com `TenantId: null`. Linhas de prova foram removidas após verificação. |
| Bootstrap vazio sem chave | API real em Development com DB novo do Compose, sem `AdminApiKey` | Passou; `/health` 200 | API iniciou; 0 tenants, 0 API keys, 0 agentes e 0 workflows. Endpoints de agente/DevUI que precisam de tenant não foram mapeados. O DB de prova foi descartado. |
| Modelo EF | `dotnet ef migrations has-pending-model-changes` | Passou | Nenhuma mudança de modelo pendente. |
| Contratos da API | OpenAPI e inventário de endpoints regenerados | Passou | 224 combinações método/rota e 219 operações MVC visíveis no OpenAPI Release. |
| Links locais de documentação | `scripts/check-documentation-links.mjs` | Passou | 165 arquivos, 889 links conferidos, 0 quebrados. |
| Formatação Git | `git diff --check` | Passou | Sem whitespace inválido. Avisos LF/CRLF do Git são apenas normalização do working tree. |

## Comportamento validado

- `default`, `platform`, `system-background` e `system-devui` são recusados pelo resolvedor/contexto; o tenant registry não os expõe como tenants ativos.
- Stores tenant-owned (sessões, quotas, workflows, golden sets e alertas) exigem contexto real correspondente e mantêm filtros de tenant.
- A seleção global da outbox usa uma capability tipada; cada evento tenant-owned é publicado e confirmado dentro do tenant persistido. Eventos globais não usam `TenantId` sintético.
- Catálogo, quota, alertas e outbox globais têm armazenamento de plataforma. BYOK, alertas e sessões de tenants não compartilham essas tabelas.
- Rotação de secrets, reset de quota, consolidação de sessão, limpeza de agentes, sincronização de conectores, tarefas agendadas e recuperação/purga ONNX enumeram tenants e processam cada um separadamente.

## Limitações

Os cinco skips são cenários condicionais de OCR/serviços externos ou uma integração RAG específica; eles não invalidam os fluxos tenant/system acima, que têm testes focados e integração PostgreSQL. O PostgreSQL usado foi exclusivamente o serviço `postgres` do Compose em `55432`; nenhuma conexão em `5432` foi usada. A2A/AG-UI e provider LLM real do Gateway continuam fora desta validação, conforme #121/#133.

## Reprodução e limpeza

Use `tests/backend-validation/compose.yml` e somente o serviço PostgreSQL em `127.0.0.1:55432`. As tabelas de validação usam IDs aleatórios e são removidas ao fim dos testes. O banco temporário `backend_bootstrap_validation` foi descartado; o container/volume de `backend_validation` foi mantido para execução das integrações do branch.
