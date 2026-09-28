# Plano — Documentação e validação do backend
Status: entregue para revisão; PR draft [#118](https://github.com/JonathanBenicio/Agent-System/pull/118)
Issue: [#110](https://github.com/JonathanBenicio/Agent-System/issues/110)
ADR: [034](../architecture/adr/034-backend-contracts-and-access-target.md)
Story: BACK-DOC-001 em [catálogo](../USER-STORIES.md)
Baseline: f8de7a6e3aa9d671a67ae60f2f52e0c1f80b3eae, 2026-09-28.
Branch local: docs/backend-contracts-validation (baseline f8de7a6).
Branch de revisão remota: docs/backend-contracts-review; base: feature/opencode-gemini.
Os 12 commits preexistentes da baseline não foram publicados por esta iniciativa. O PR recebe somente arquivos desta entrega; alinhar a base com a baseline antes de merge e de reproduzir diagnósticos a partir do PR.

## Objetivo e limites
Consolidar endpoints/regras/tenants/recursos/evidências e melhorar/executar o processo. Sem mudanças de produção/schema, merge/deploy. Preservar alterações do usuário.

## Etapas
- [x] Criar epic antes dos artefatos e registrar baseline.
- [x] Melhorar templates e workflow, incluindo templates nativos de issue/PR.
- [x] Inventariar HTTP/SSE/hubs/protocolos e publicar contratos: 201 rotas de controllers, 196 visíveis em OpenAPI Release, sem divergência.
- [x] Executar diagnóstico com PostgreSQL/pgvector e Ollama isolados, incluindo restart da API.
- [x] Registrar resultados, cobertura, limitações e backlog #111–#117: 24 passaram, 9 falharam, 2 não executados; cobertura 22,39%.
- [x] Sincronizar índices/links: 631 links locais verificados, zero destinos quebrados no escopo vivo.
- [x] Entregar commits/PR draft rastreável: [#118](https://github.com/JonathanBenicio/Agent-System/pull/118), commits locais 8aae1da/6457104 e de revisão remota be058e9/ce0dfe7, todos com Refs #110.

## Verificação
Links e exemplos confrontados com código; inventário regenerável. Build/suíte/cobertura separados de estabilidade integrada. Cenários: auth/troca de tenant, salas/ACL/RAG, sessões/chat/SSE/SignalR, limites e persistência. Dois tenants e identidades sintéticas. Serviços reais; sem mocks de LLM/PG.

## Riscos e entrega
Download/recursos/startup podem limitar diagnóstico: reportar falhou/não executado/capacidade ausente separadamente. Não usar serviços/volumes/chaves do usuário. Falhas fora do escopo geram backlog. [Hub](../backend/README.md), [relatório](../backend/validation/2026-09-28.md), templates e PR com evidências sanitizadas.

## Resultado e próximos passos
Nenhum código/schema de produção alterado. Falhas centrais reproduzidas em API key, query de tenant SignalR, SQL/guard de RAG, chat MAF, concorrência de quotas e seeding de skills. [Backlog](../backend/backlog.md) define ordem e critérios. Memberships/suporte/limites unificados permanecem desenho futuro. A conclusão desta auditoria não certifica estabilidade do backend nem autoriza merge/deploy.

O checkout remoto do PR ainda possui referências a fontes/planos existentes somente na baseline local, além de um link file:/// preexistente da base. Seu checker não passa antes de alinhar a base. A verificação de 631 links/zero quebrados refere-se exclusivamente à revisão local validada. PR não é certificado como reprodutível em b681722. Serviços isolados foram parados; volumes e evidências locais conservados.
