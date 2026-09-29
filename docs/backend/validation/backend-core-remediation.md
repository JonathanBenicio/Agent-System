# Validação — isolamento e funcionalidades centrais do backend

Data: 2026-09-28/29 · Branch: `fix/backend-core-tenancy` · Plano: [backend-core-remediation](../../plan/backend-core-remediation.md) · ADR: [035](../../architecture/adr/035-backend-core-isolation-and-reliability.md). Este relatório registra a validação local da branch; não substitui o relatório histórico de [documentação](2026-09-28.md).

## Resultado atual

API Release e harness de diagnóstico compilam com zero avisos e erros. PostgreSQL 16.15/pgvector 0.8.6 e Ollama foram executados localmente em ambiente isolado, com credenciais e dados sintéticos.

| Verificação | Resultado | Evidência |
|---|---:|---|
| `AgenticSystem.Tests` Release | 697 aprovados, 1 ignorado, 0 falhas (698 total) | TRX em `tests/TestResults/backend-core-gap-closure/run-2026-09-28/coverage/backend-core-gap-closure.trx`. O teste ignorado depende de PostgreSQL no contexto de teste convencional. |
| Cobertura Cobertura global da coleta | **21,08% de linhas** (12.729/60.359); 25,71% de branches | XML em `tests/TestResults/backend-core-gap-closure/run-2026-09-28/coverage/38b0c3b3-237d-4efd-9d3f-2dc19f0a475c/coverage.cobertura.xml`. A meta declarada no CI é 80% e não foi atendida. A PR deve permanecer draft até a meta ser alcançada ou o contrato de CI ser corrigido numa mudança aprovada. |
| Integração HTTP/SignalR/PostgreSQL/Ollama | 39 aprovados, 0 falhas | `core-results.json`: auth e membership, ACL/support grants, agentes, quotas de sala/documento, ingestão, RAG real, sessão, REST/SSE, hubs e rate limit. |
| Store, skills e quotas | 10 aprovados, 0 falhas | Inclui 32 atualizações concorrentes em dois repositórios e factories de contexto independentes; limites persistidos, reset UTC, busca vetorial/room. |
| Backfill de membership legado | aprovado | Fixture inicia no schema anterior à migration, insere role assignment e API key, aplica migrations pendentes e verifica tenant/papel/grantor e ausência de Platform Admin. Banco temporário foi descartado ao final. |
| Continuação de sessão após restart | aprovado | Chat real retomou o mesmo `sessionId`; o estado MAF persistido foi desserializado e o marcador de restauração sobreviveu no store. Mensagens e ownership também sobreviveram. |
| Auditoria de dependências | sem pacotes vulneráveis conhecidos | `dotnet list AgenticSystem.sln package --vulnerable --include-transitive`, fontes NuGet atuais na execução. |

Artefatos de runtime ficam sob `tests/TestResults/backend-core-gap-closure/run-2026-09-28/` e são ignorados pelo Git. O baseline documental anterior permanece em `tests/TestResults/backend-documentation/current`; esses resultados não foram sobrescritos.

## Critérios dos gaps

| Gap | Estado | Evidência/limite |
|---|---|---|
| #111 — identidade/API keys | Resolvido nesta branch | API key usa papel da membership no tenant, papel inválido falha fechado; seleção de tenant inválida é negada. |
| #112 — isolamento HTTP e hubs | Parcial | O mesmo subject conectado em dois tenants recebeu eventos de Chat, ExternalAgent, Workflow e ONNX somente no tenant autorizado. Gateway confirmou resposta direta restrita à conexão invocadora e o grupo tenant-scoped no código; não havia serviço registrado para disparar o broadcast de estado real. |
| #113 — RAG por sala | Resolvido nesta branch | Ingestão associada a sala com ACL; chat real recuperou o chunk autorizado, registrou referência de artefato ao chunk e respondeu a frase sintética indexada. Tenant B não viu chunks. |
| #114 — estado MAF durável | Resolvido nesta branch | Continuação de conversa após restart restaurou estado MAF e conservou o mesmo ID; ownership tenant/usuário verificado. |
| #115 — quota PostgreSQL | Resolvido para concorrência testada | 32 incrementos via duas instâncias independentes persistiram exatamente uma vez, além de limite, reset UTC e teto do plano. O teste usa duas instâncias de repositório/contexto no mesmo processo, não dois processos/hosts de API. |
| #116 — catálogo de skills | Resolvido nesta branch | Defaults estáveis por tenant, IDs/customizações preservados e concorrência de catálogo exercitada. |
| #117 — administração e migration | Resolvido para os cenários da fixture | Backfill legado preserva tenant e papel sem criar admin de plataforma. Rotas GET/PUT/DELETE de memberships são admin-only, auditadas e sincronizam compatibilidade de papel. Fixture é sintética e não uma cópia representativa de produção. |
| Limites de recursos | Resolvido para controles cobertos | Sessões, agentes, documentos lógicos e bytes de origem usam `Tenant.Limits`; plano é teto e configuração pode restringir. Bytes não representam uso físico total em disco; custo mensal legado é projeção de custo diário × 30. |
| Meta de cobertura CI | **Não resolvida** | Coleta completa mediu 21,08%, abaixo de 80%. Nenhum limite foi reduzido. A entrega não pode ser considerada integralmente pronta para merge enquanto esse gate continuar obrigatório. |

O evento FinOps `TurnCostUpdated` é emitido por `QuotaEnforcer.RecordUsageAsync` depois da persistência, com erro do publisher isolado; o diagnóstico unitário cobre esse comportamento, mas não foi provado que todos os caminhos de chat chamem `RecordUsageAsync`. A documentação não afirma que o evento esteja conectado a todo consumo real.

## Reprodução

Com os serviços locais de validação isolados ativos:

```powershell
$env:BACKEND_VALIDATION_OUTPUT_DIR = (Join-Path $PWD 'tests/TestResults/backend-core-gap-closure/run-2026-09-28')
node tests/backend-validation/core-diagnostics.mjs
dotnet tests/backend-validation/bin/Release/net10.0/BackendDiagnostics.dll
dotnet tests/backend-validation/bin/Release/net10.0/BackendDiagnostics.dll --legacy-backfill
```

Após a criação das fixtures de sessão, reinicie somente a API isolada e rode:

```powershell
node tests/backend-validation/session-diagnostics.mjs --after-restart
```

Suíte e cobertura:

```powershell
dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --no-restore --configuration Release --logger 'trx;LogFileName=backend-core-gap-closure.trx' --collect:'XPlat Code Coverage' --results-directory 'tests/TestResults/backend-core-gap-closure/run-2026-09-28/coverage' --verbosity minimal
```

Os scripts usam PostgreSQL/Ollama do compose local de validação e a API isolada em `127.0.0.1:5188`; não usam dados nem endpoints de produção. `stop-api.ps1` interrompe somente o processo/API de validação e preserva serviços/volumes.
