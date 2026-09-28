# Plano — Corrigir isolamento e funcionalidades centrais
Status: implementação concluída para os fluxos listados; validação de alguns critérios e entrega ainda pendentes · Issues: #111–#117 · [ADR-035](../architecture/adr/035-backend-core-isolation-and-reliability.md) · Stories: BACK-FIX-111–117
Baseline: 6039df6, 2026-09-28 · Branch: fix/backend-core-tenancy · Base de PR: docs/backend-contracts-review
Alterações preexistentes preservadas: `.gitignore` staged e arquivos/pastas não rastreados do usuário.

## Escopo e autorização
Corrigir backend/migrations, documentar contratos e provar os fluxos no PostgreSQL/pgvector/Ollama isolados. Alvo ADR-034 confirmado pelo usuário. Migração preserva tenant/papel sem promoção de Platform Admin. Plano é teto; limite configurado pode restringir. Sem merge/deploy ou mudança incidental de frontend.

## Implementação e validação
- [x] Leitura dos bugs e causas; ADR, stories, plano e branch isolada registrados.
- [x] #111: role persistida de API key, roles desconhecidas negadas, membership consulta role no tenant e troca de tenant negada. Auth API key/JWT real e testes unitários passaram.
- [~] #112: tenant validado em HTTP e SignalR, query spoof/tenant desconhecido negados; grupos de chat/gateway/workflow/external-agent/ONNX e eventos FinOps/Workflow incluem tenant. Handshake de Gateway/chat aprovado; entrega cruzada concorrente em cada hub permanece pendente.
- [x] #113: SQL parametrizado usa metadata/tenant; lista vazia falha fechada; busca PostgreSQL real encontrou sala autorizada entre 61 documentos candidatos e isolou tenant. Pipeline completo de resposta RAG vinculada a sala permanece sem prova end-to-end.
- [~] #114: chave MAF combina tenant/NameIdentifier; escrita propaga falhas; REST, SSE e SignalR geraram sucesso; ownership negou outros usuários/tenants; registros sobreviveram ao restart da API. Restauração do estado serializado interno do MAF após restart não foi exercitada.
- [x] #115: upsert e reset atômicos no PostgreSQL; oito incrementos concorrentes persistiram, reset/rollover confirmado e limite de plano recusou quota configurada acima do teto. Multi-processo/multi-instância não testado.
- [x] #116: defaults isolados por tenant, IDs estáveis, seed completa catálogo parcial, conserva customização legada e duas cargas concorrentes completam quatro entries.
- [~] #117: migration cria membership/admin/grant e faz backfill; API lista tenants, muda plano sem apagar restrições configuradas e cria/lista/revoga suporte Reader temporário com motivo, expiração <=7d e auditoria de grant/acesso. Admin sem grants não acessa conteúdo. Falta teste do backfill numa cópia da base legada e superfície administrativa de memberships/roles não foi adicionada.
- [x] Atualizar inventário de rotas: 206 combinações, sem duplicatas; documentar grants, planos, recursos e enforcement.
- [x] Build API Release, EF sem mudanças pendentes, suíte unitária e integração isolada concluídos.
- [ ] Entrega rastreável: revisar diff/staging, commits vinculados às issues, sincronizar remote/PR e anexar os resultados antes de fechar issue; não mesclar nem implantar.

## Resultados conferidos
API build: sem avisos/erros. `dotnet test tests/AgenticSystem.Tests/AgenticSystem.Tests.csproj --no-restore --configuration Release --verbosity minimal`: 689 passed, 1 skipped, 0 failed (690 total). PostgreSQL 16.15/pgvector 0.8.6/Ollama: core diagnostics 29/29; 9/9 store/quota/skills incluindo busca por sala entre 61 candidatos. Build do harness também reportou NU1903 em Microsoft.OpenApi 2.4.1 e SQLitePCLRaw.lib.e_sqlite3 2.1.11; dependências não foram atualizadas neste escopo.

Resultados detalhados: [relatório](../backend/validation/backend-core-remediation.md). Evidência histórica do diagnóstico documental continua em [2026-09-28](../backend/validation/2026-09-28.md) e não deve ser sobrescrita.

## Critério de fechamento
Cada issue só pode ser fechada quando seu aceite obrigatório estiver coberto por código e evidência. Permanecem especialmente pendentes: entrega cross-hub, estado serializado MAF, fluxo RAG de ponta a ponta, cópia legada de backfill, membership/role admin, testes multi-instância e unificação de limites de recursos. Cobertura global requerida pelo CI deve ser medida separadamente; esta execução não afirma que 80% foi alcançado.