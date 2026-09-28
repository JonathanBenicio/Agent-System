# Validação — correção de isolamento e confiabilidade do backend

Data: 2026-09-28 · Branch: `fix/backend-core-tenancy` · Plano: [backend-core-remediation](../../plan/backend-core-remediation.md) · ADR: [035](../../architecture/adr/035-backend-core-isolation-and-reliability.md). Estes resultados são desta branch e não substituem o relatório histórico de [documentação](2026-09-28.md).

## Resultado

A API Release compilou com 0 avisos e 0 erros. A suíte `AgenticSystem.Tests` passou 689 testes, ignorou 1 teste dependente de PostgreSQL e teve 0 falhas (690 total). PostgreSQL 16.15/pgvector 0.8.6 e Ollama reais foram usados em um ambiente local isolado, com credenciais e dados sintéticos. Migration nova apareceu no histórico EF do banco de teste (7 migrations aplicadas).

| Diagnóstico | Resultado | O que foi verificado |
|---|---:|---|
| HTTP/SignalR (`core-diagnostics.mjs`) | 29 passaram, 0 falharam | Auth, membership, tenant spoofing, ACL, admin de plataforma sem acesso implícito, troca de plano mantendo limites, grants criados/lidos/auditados/expirados/revogados, Ollama, embeddings, isolamento por tenant, chat REST/SSE/SignalR e rate limit. |
| Store/quota/skills (`BackendDiagnostics.dll`) | 9 passaram, 0 falharam | Tenant e sala filtrados no SQL, zero resultados sem salas permitidas, 61 candidatos de sala no PostgreSQL real, defaults de skill estáveis/isolados/concurrentes, quota persistida, incrementos concorrentes, reset UTC e teto Free sobre configuração maior. |
| Sessão após restart | passou em execução separada | Mensagens sintéticas conhecidas persistidas no store PostgreSQL sobreviveram a parada/reinício seguro da API; owner manteve leitura e usuário/tenant diferentes receberam 404. Não prova restore do estado serializado interno do MAF. |
| EF | passou anteriormente nesta branch | `migrations has-pending-model-changes`: sem mudanças pendentes; migration aplicada no banco isolado. |

Saídas sanitizadas da execução corrente estão em `tests/TestResults/backend-core-remediation/run-2026-09-28/`; os resultados brutos permanecem ignorados pelo Git. As verificações não usaram mocks de PostgreSQL, pgvector ou LLM. O teste de 61 candidatos confirma o prefilter do store, não um fluxo RAG completo até a resposta do agente.

## Lacunas restantes

- #112: testar entrega concorrente e negar vazamento entre tenants em cada hub (Gateway, Chat, ExternalAgent, Workflow e ONNX); os testes atuais exercitaram negociação/invocação Gateway e Chat e rejeição de tenant divergente.
- #113: chamar chat com uma sala autorizada e confirmar contexto RAG efetivamente usado, além do teste direto do store e ACL.
- #114: testar restauração do estado MAF serializado após reinício; o teste após restart prova registros/mensagens do store, não checkpoint interno do framework.
- #117: repetir backfill numa cópia representativa dos dados legados; hoje a migration foi aplicada e o bootstrap de memberships é verificado, mas o banco isolado não continha amostra legada pré-migration. Não há endpoints para gestão administrativa de memberships/roles.
- #115 e limites de recursos: oito incrementos concorrentes e reset persistido passaram em um processo/conexões independentes; duas instâncias simultâneas não foram testadas. Sessões/agentes/storage continuam em `TenantResourceLimits`, fora da quota unificada.
- Os cinco hubs ainda não tiveram um teste de entrega cruzada concorrente por evento/grupo.

O build do harness de validação emitiu NU1903 para Microsoft.OpenApi 2.4.1 e SQLitePCLRaw.lib.e_sqlite3 2.1.11 (dependências existentes); não houve atualização incidental de dependências. A suíte desta execução não mede nem afirma a cobertura global mínima de 80%. Frontend não foi alterado, portanto lint/build/E2E de frontend não se aplicam.

## Reprodução

Com os serviços locais isolados ativos:

```powershell
$env:BACKEND_VALIDATION_OUTPUT_DIR = (Join-Path $PWD 'tests/TestResults/backend-core-remediation/run-2026-09-28')
node tests/backend-validation/core-diagnostics.mjs
dotnet tests/backend-validation/bin/Release/net10.0/BackendDiagnostics.dll
dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --no-restore --configuration Release --verbosity minimal
```

O script SQL usa explicitamente o projeto compose `agent-system-backend-fix` e só conecta ao PostgreSQL em `127.0.0.1:55432`. Os scripts seguros de start/stop da API conferem a DLL e listener `5188`; não removem containers nem volumes.