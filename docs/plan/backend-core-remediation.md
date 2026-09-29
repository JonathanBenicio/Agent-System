# Plano — Corrigir isolamento e funcionalidades centrais

Status: implementação dos gaps concluída nesta branch; release bloqueado pela cobertura abaixo de 80% e prova de broadcast Gateway pendente · Issues: #111–#117 · [ADR-035](../architecture/adr/035-backend-core-isolation-and-reliability.md) · Stories: BACK-FIX-111–117

Baseline: `6039df6`, 2026-09-28 · Branch: `fix/backend-core-tenancy` · Base de PR: `docs/backend-contracts-review`.

## Escopo e decisões

Corrigir backend/migrations, documentar contratos e comprovar os fluxos em PostgreSQL/pgvector/Ollama isolados. Aplicar ADR-035: preservar tenant/papel existente sem promover alguém a Platform Admin; plano é teto e limite configurado pode restringir. Não fazer merge/deploy nem alterar frontend. Arquivos previamente alterados do usuário devem permanecer fora da entrega.

## Implementação e validação

- [x] #111 — membership determina papel no tenant; API keys usam role persistida e papéis desconhecidos falham fechado.
- [~] #112 — identidade/tenant validados em HTTP/SignalR. Mesmo subject em dois tenants recebeu eventos de Chat, ExternalAgent, Workflow e ONNX somente no tenant autorizado. Gateway provou resposta direta só à conexão invocadora e grupos tenant-scoped; sem serviço registrado, não foi possível produzir evento real do broadcaster.
- [x] #113 — busca SQL pré-filtra tenant/salas permitidas. Ingestão em sala com ACL e chamada real de chat provaram uso do chunk no artefato RAG e na resposta; tenant B não teve acesso.
- [x] #114 — após reinício da API, chat real continuou com o mesmo ID e o estado MAF foi desserializado, confirmado por marcador persistido; isolamento e mensagens sobreviveram.
- [x] #115 — quotas/reset atômicos PostgreSQL; 32 incrementos via dois repositórios/context factories independentes persistiram exatamente uma vez. Dois processos/hosts de API não foram testados.
- [x] #116 — catálogo de skills preserva IDs/customizações, prepara defaults estáveis por tenant e passou o cenário concorrente.
- [x] #117 — fixture inicia no schema legado com role assignment e API key; migration preserva tenant/papel/grantor e não cria Platform Admin. Rotas admin-only GET/PUT/DELETE de membership foram implementadas e auditadas.
- [x] Limites — `Tenant.Limits` é fonte única do teto de sessões, agentes, documentos lógicos e bytes de origem; plano limita para cima e configuração pode restringir.
- [x] Pacotes — versões corrigidas; auditoria NuGet atual não encontrou pacotes vulneráveis conhecidos.
- [ ] Cobertura CI — 21,08% de linhas medidas contra exigência declarada de 80%. Não reduzir gate para aprovar a branch.

## Resultados conferidos

Build Release da API e do harness: zero avisos/erros. Suíte: 697 aprovados, 1 ignorado, 0 falhas (698 total). Integração HTTP/SignalR/PostgreSQL/Ollama: 39/39 aprovados. Store/quota/skills: 10/10. Backfill legado e continuação/restauração MAF após restart: aprovados. Cobertura: 12.729/60.359 linhas (21,08%), 25,71% branches; gate de 80% não satisfeito.

Resultados e limitações: [relatório de validação](../backend/validation/backend-core-remediation.md). Evidência histórica do diagnóstico documental continua em [2026-09-28](../backend/validation/2026-09-28.md) e não deve ser sobrescrita. Saídas da execução corrente ficam em `tests/TestResults/backend-core-gap-closure/run-2026-09-28/` e são ignoradas pelo Git.

## Critérios de conclusão

Os critérios de código para #111, #113–#117 e de isolamento de quatro hubs estão cobertos nesta branch. #112 continua parcial para evento broadcast real de Gateway. A fixture de migration é sintética, não uma cópia representativa de produção. Bytes de origem medem conteúdo recebido atribuído a documento lógico, não armazenamento físico total. `MaxMonthlyBudgetUsd` legado é projeção de custo diário × 30, não acumulador mensal. O evento FinOps é testado no `QuotaEnforcer`, mas a ligação de `RecordUsageAsync` a todos os caminhos reais de chat não foi provada.

Concluir o gate de release exige cobertura global ≥80% e uma prova de broadcast Gateway ou documentação explícita de seu limite. Manter PR em draft enquanto o gate obrigatório falhar. Não fechar issue, fazer merge ou deploy sem que os critérios correspondentes tenham sido verificados.

## Próximas ações

- [x] Sincronizar contratos de recursos, membership, sessões/hubs e evidências com código e execução.
- [ ] Elevar cobertura global até 80%, sem excluir código relevante para inflar o denominador.
- [ ] Adicionar cenário que provoque broadcast de estado Gateway com evento/serviço de fixture.
- [ ] Revisar diff e staging; preservar `.gitignore` e arquivos/pastas não rastreados preexistentes do usuário.
- [ ] Atualizar Issues #111–#117 e PR #119 com resultados e limitações; manter draft enquanto gate estiver bloqueado.
