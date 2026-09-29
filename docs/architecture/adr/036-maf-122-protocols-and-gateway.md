# ADR-036 — Atualização do MAF e integração do Gateway

Data: 2026-09-29 · Issue: [#120](https://github.com/JonathanBenicio/Agent-System/issues/120) · Story: BACK-MAF-120 · [Plano](../../plan/maf-122-protocols-gateway.md). Validação E2E A2A/AG-UI foi separada para [#121](https://github.com/JonathanBenicio/Agent-System/issues/121), [ADR-037](037-a2a-agui-preview-validation.md).

Decisão: aceita · Implementação: parcial no worktree, sem commits · Validação: build Release (0 avisos/erros) e suíte com Compose isolado (726 aprovados, 1 teste vetorial explicitamente ignorado) passaram. Integração PostgreSQL verificou migration, armazenamento cifrado/auditoria, leitura entre tenants, propagação NOTIFY pelo listener ao notifier e snapshot MAF salvo/reaberto após recriar adapter, negando outro tenant. Atualização efetiva em outro LLMManager/host, restart real de sessão e provider real ainda não foram demonstrados. DurableTask continua incompatível com grafos dinâmicos e com polling atual.

## Contexto

A baseline usa Microsoft Agent Framework 1.9.0, hosting 1.9.0-preview, Microsoft.Extensions.AI 10.6.0 e APIs de hosting A2A/AG-UI em preview. O ambiente Validation desliga ambos os protocolos; a produção cria `IServiceGateway`, porém não registra providers ativos nem encaminha chamadas de chat por ele. A release .NET 1.22.0 introduz mudanças relevantes em sessões, replay/aprovações, MCP e A2A, por isso a mudança deve ser analisada antes dos gaps funcionais. Como o hosting A2A/AG-UI continua preview, sua validação E2E não bloqueia este trabalho e foi separada na issue #121.

O código atual persiste as alterações do `LLMController` por `ConfigManager`/`PostgresConfigStore`. `ConfigEntryEntity` é `ITenantEntity` e tem unicidade `(TenantId, Key)`, enquanto `LLMManager` muta um catálogo singleton. Portanto, a configuração global depende do tenant que a gravou/carregou e não está garantida após restart ou em múltiplos nós; a decisão abaixo requer armazenamento global separado de BYOK.

O código atual persiste a configuração alterada por `LLMController` através de `ConfigManager`/`PostgresConfigStore`, cuja entidade `ConfigEntryEntity` é `ITenantEntity` e usa índice único `(TenantId, Key)`. O endpoint passou a ser reservado a Platform Admin e altera providers mantidos em singleton. Isso mistura escopo global de runtime com persistência tenant-scoped, portanto não garante consistência após restart, em múltiplos nós ou quando outro tenant for o primeiro a carregar configuração.

## Decisão

- Atualizar os pacotes `Microsoft.Agents.*` utilizados para a família 1.22.0, usando versões preview correspondentes para componentes de hosting necessários. Não presumir igualdade de versão entre pacotes; confirmar dependências e compatibilidade via NuGet/restore. Esta mudança não promove A2A/AG-UI a contratos estáveis.
- Alinhar `Microsoft.Extensions.AI` e demais dependências transitivas aos requisitos publicados pelos pacotes MAF selecionados. A análise inicial encontrou M.E.AI 10.10.0, `Microsoft.Extensions.*` 10.0.12, `Microsoft.Extensions.VectorData.Abstractions` 10.10.0 e `OpenTelemetry.Api` 1.18.0 como mínimos de alguns hosts. Não atualizar bibliotecas sem relação direta sem necessidade demonstrada.
- Preservar ownership, isolamento tenant/usuário, persistência e serialização das sessões durante execução, retomada e restart. O adaptador deve honrar todo o `AgentSessionStoreKey`, inclusive partições; importar estado legado somente após confirmar owner/tenant persistidos.
- Manter a fábrica que materializa agentes e grafos declarativos do banco: isso implementa customização dinâmica por tenant/agente; `ChatClientAgent` e `WorkflowBuilder` do MAF são o runtime usado pela plataforma, enquanto o catálogo, políticas, tools e definição versionada continuam sendo domínio do produto.
- Não substituir agora `ContextAwareChatClient` por `RoutePersistingRoutingChatClient`. O recurso do MAF persiste a rota selecionada em `AgentSession` e permite alternar entre clients preservando histórico cliente-side; o produto também resolve credenciais tenant/BYOK, modelo, fallback, quota persistida e auditoria. A migração exigiria demonstrar como essas políticas, especialmente por tenant, ficam equivalentes.
- Administração de providers globais e mudança do provider default são operações de plataforma; os endpoints de `LLMController` exigem Platform Admin explícito. Chaves BYOK e seus recursos continuam sob isolamento/quota do tenant. Configuração global persistida precisa de store de plataforma sem tenant, com segredo criptografado, trilha de auditoria por actor e notificação de recarga aos nós; `ConfigEntryEntity`/`SystemStateEntity` são tenant-scoped. Auditoria guarda hash antes/depois, nunca segredo em texto puro.
- Manter A2A/AG-UI sob suas feature flags e sem ampliação de escopo/configuração nesta issue. Validar seus fluxos E2E posteriormente conforme ADR-037 e issue #121.
- Registrar no startup de produção apenas providers de infraestrutura configurados e habilitados. Chamadas completas e streaming desses providers passam por `IServiceGateway`; estados de circuit breaker, rate limit e saúde refletem execução real. Credenciais BYOK de tenant continuam no caminho atual e fora do registro global, pois os controles do Gateway são globais por provider e poderiam permitir que uma chave afete outros tenants. Quotas persistidas continuam autoridade por tenant. Custo estimado do Gateway não representa custo real por token.
- Uma migration é necessária para armazenamento global de configurações e sua auditoria. Ela preserva as configurações tenant-scoped atuais e não promove credenciais tenant para globais. A API não retorna segredos; BYOK continua em tabela e quota tenant-scoped. Se aparecer outra mudança de modelo, parar e revisar antes de gerar migration.

## Alternativas e trade-offs

- Permanecer em 1.9.0 reduz risco imediato, mas deixa sem suporte aos novos contratos compartilhados de sessão e impede validar a rota real atualizada do Gateway/runtime.
- Atualizar sem matriz de compatibilidade foi rejeitado: breaking changes documentadas afetam sessões, aprovações/replay, MCP e A2A.
- Trocar o roteador multi-tenant do produto pelo roteador de sessão do framework agora foi rejeitado: os escopos se sobrepõem parcialmente e a paridade de credenciais/quotas ainda não foi demonstrada.
- Registrar providers apenas para o dashboard não prova que o Gateway governa tráfego real; a decisão integra chamadas completas e streaming, aceitando a extensão do contrato público de `IServiceGateway`.

## Consequências e migração

A atualização pode exigir adaptações de API em adaptadores MAF e código de armazenamento de sessão. Componentes de hosting permanecem preview e exigem testes de protocolo. O contrato de `IServiceGateway` ganha operação de streaming que contabiliza falhas durante enumeração, cancelamento e latência real; consumidores existentes de chamadas completas permanecem compatíveis. A configuração global de providers deve ser separada de configuração BYOK por tenant; o store atual é tenant-scoped e não satisfaz a decisão de plataforma. Rollback de código consiste em reverter os commits por contexto; qualquer dado global novo deve ter migração/rollback explícitos.

## Verificação e gaps

Aceite arquitetural não prova implementação. Build Release, testes de sessão, streaming/cancelamento/fallback e Gateway com providers de infraestrutura habilitados são os critérios deste escopo. BYOK continua preservado e explicitamente não governado pelo Gateway global. Para A2A/AG-UI, este trabalho só verifica compatibilidade de dependências/build; E2E, autorização e isolamento continuam pendentes em #121. PostgreSQL/Ollama indisponíveis ou qualquer teste não executado devem ser reportados como gaps, sem substituir evidência integrada por teste unitário.
