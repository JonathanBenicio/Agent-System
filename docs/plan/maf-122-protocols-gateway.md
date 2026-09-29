# Plano — Atualizar MAF e integrar providers ao Gateway

Status: em execução · Issue: [#120](https://github.com/JonathanBenicio/Agent-System/issues/120) · [ADR-036](../architecture/adr/036-maf-122-protocols-and-gateway.md) · Story: BACK-MAF-120.

Baseline: `f941198` · 2026-09-29 · Branch: `fix/backend-core-tenancy`. Este plano fica dedicado ao upgrade MAF/Gateway; a implementação semântica do supervisor está separada em [dynamic-orchestrator-implementation.md](dynamic-orchestrator-implementation.md), com regressões unitárias/funcionais locais aprovadas e validação PostgreSQL de sessão ainda pendente. A2A/AG-UI E2E está em #121/ADR-037. `.gitignore` staged e arquivos pessoais preexistentes continuam fora dos commits.

Análise de breaking changes e matriz das APIs do repositório: [maf-122-compatibility-review.md](maf-122-compatibility-review.md). Concluída em 2026-09-29 antes do bump; restore/build Release com versões de destino passam.

## Objetivo e escopo

Atualizar os pacotes MAF efetivamente usados após revisar mudanças incompatíveis; preservar o contrato tenant-scoped de sessão; registrar e encaminhar providers globais pelo Gateway em produção. A implementação funcional do supervisor e a consolidação das sessões de especialistas ficam no plano separado do orquestrador. Para A2A/AG-UI, somente compatibilidade/build; E2E em #121. Sem merge, deploy, mudança no frontend ou redução do gate de cobertura.

## Etapas

| Entrega | Dependência | Verificação | Estado/evidência |
|---|---|---|---|
| Issue, ADR, story e rastreabilidade | Nenhuma | Links cruzados e índices sincronizados | #120 atualizado no GitHub com estado de execução, gap de registry/polling e evidência da suíte; #121 e #122 separados |
| Matriz de compatibilidade 1.9.0 → 1.22.0 | Issue/ADR/story | APIs usadas comparadas às releases oficiais; requisitos NuGet registrados | Concluída em `maf-122-compatibility-review.md` |
| Atualização coordenada dos pacotes e sessão | Matriz concluída | Restore/build Release, partições de sessão, migrations | MAF/session-store compilam; suíte completa Release com conexão PostgreSQL isolada: 738 aprovados, 1 skip vetorial explicitamente marcado, 0 falhas; build Release: 0 avisos/erros. Testes PostgreSQL provaram snapshot de sessão MAF após recriar adapter e execução com snapshot/version/hash de definição mesmo após editar a versão viva. DurableTask continua incompatível com grafos dinâmicos e será removido dessa rota. |
| Compatibilidade de hosting A2A/AG-UI | Build atualizado | Mapeamento com `ValidateScopes=true`, sem alegar E2E | 2 testes de registro passaram; E2E em #121 |
| Registry e caminho de execução Gateway | MAF atualizado | Startup/runtime, chamadas completas/streaming, falha/cancelamento/fallback; BYOK isolado | Caminho `ContextAwareChatClient` → Gateway → provider fake passou para resposta e streaming; startup consulta configuração global persistida antes de registrar providers. Provider LLM real e fallback de rede ainda não exercitados |
| Separar configuração global de provider e BYOK | Decisão de escopo Platform Admin | Dois tenants veem o mesmo provider global após restart/nó novo; alteração global registrada com ator; chave BYOK de um tenant continua privada | Implementado parcialmente: entidades/store globais sem `TenantId`, segredos cifrados/auditados por hash, gravação transacional e NOTIFY. Teste no PostgreSQL do Compose confirmou leitura entre dois tenants, ciphertext/auditoria e propagação de NOTIFY pelo `RealTimeConfigReloadBackgroundService` até o notifier do host. Falta validar alteração efetiva do LLMManager entre dois hosts/provedores reais. Valores legados ficam tenant-scoped, sem promoção automática |
| Verificação final | Etapas anteriores | Suíte, EF pending-model, PostgreSQL/Ollama e revisão de diff | Build Release: 0 avisos/erros; suíte completa Release com PostgreSQL isolado: 738 aprovados, 1 skip vetorial explicitamente marcado, 0 falhas; EF sem mudanças pendentes; link checker: 154 documentos/729 links sem quebras. Migrations aplicadas no volume descartável `agent-system-backend-validation-20260929`. Recuperação automática após restart e workers concorrentes ainda pendem. |

## Limite com o plano do orquestrador

Este plano não fecha o comportamento funcional do supervisor, o prompt dinâmico, a resolução do especialista chamado nem a persistência das sessões dos especialistas. Esses itens estão isolados em [BACK-ORCH-122](dynamic-orchestrator-implementation.md), com ADR-038 e story própria. Os arquivos de orquestrador já alterados no worktree permanecem parciais até concluir aquele plano; commits devem separar os dois contextos.

## Critérios de aceite

- [ ] Pacotes e APIs atualizados para conjunto compatível com MAF 1.22.0; `Microsoft.Extensions.AI` e dependências seguem mínimos exigidos pelos hosts efetivamente usados.
- [ ] Sessões MAF continuam isoladas por todas as partições de AgentSessionStoreKey, serializam e retomam estado após restart; estado legado só migra após validar owner/tenant.
- [ ] Hosting A2A/AG-UI compila com o grafo de dependências atualizado; validação E2E e certificação de autorização/isolamento permanecem como follow-up #121.
- [ ] Providers de infraestrutura habilitados na configuração do host são registrados e usados via Gateway para chamadas completas e streaming; providers desabilitados não são registrados. Chaves BYOK mantêm a rota atual e quotas por tenant, sem circuit/rate state global compartilhado.
- [ ] Falhas durante o stream, cancelamento e fallback atualizam métricas/estado de maneira consistente; quotas do PostgreSQL seguem a fonte do enforcement por tenant.
- [ ] Só Platform Admin explícito pode alterar configuração/defaults globais dos providers; credenciais BYOK permanecem tenant-scoped.
- [ ] Configuração global persistida usa armazenamento de plataforma sem `TenantId`, criptografa segredos e audita alterações. `ConfigEntryEntity` e `SystemStateEntity` são `ITenantEntity` e não servem como fonte global; restart e múltiplas instâncias resolvem o mesmo valor independentemente do tenant da primeira requisição.
- [ ] Store global separa dados de provider (enabled/model/priority/model catalog/default) dos segredos BYOK guardados em `ProviderApiKeys`; atualizações exigem actor Platform Admin, auditoria guarda somente hashes e a API nunca retorna o valor em texto puro.
- [ ] Precedência por request: chave/modelo explícito ou sessão, configuração BYOK/key legada tenant-scoped, depois configuração global; a chave tenant não altera nem compartilha circuit/rate state global.
- [ ] Migration de plataforma é explícita, mantém segredos cifrados, não promove valores de tenants a globais e não altera BYOK; build e testes executados e resultados reais registrados.

## Validação

Usar testes unitários para regressões do contrato e host/harness integrado para protocolos/Gateway. Testes PostgreSQL devem apontar ambas as variáveis somente para `127.0.0.1:55432/backend_validation/validation` do Compose isolado; usar `-p` específico por execução para separar containers/volumes e conferir o mapeamento de porta antes de aplicar migrations. Registrar comandos, versão/ambiente, aprovação, falha, skip e não execução separadamente. Cobertura é reportada como métrica e gate existente, não como objetivo principal da entrega.

## Riscos e gaps

For the pinned `Microsoft.Agents.AI.DurableTask` 1.16.0-preview.260922.1, `DurableWorkflowOptions.MaxSupersteps` is configurable (host setting `AgenticSystem:LocalExecution:DurableWorkflowMaxSupersteps`, default 100), and exceeding it throws `MaxSuperstepsExceededException` when work remains. This avoids silent success on the installed package, but does not solve the missing PostgreSQL worker or static workflow registration mismatch.

### Limite do checkpoint MAF para workflows dinâmicos

Os samples .NET oficiais [CheckpointAndRehydrate](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Checkpoint/CheckpointAndRehydrate) e [AotCheckpointing](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Declarative/AotCheckpointing) mostram o modelo de checkpoint/rehydration do runtime MAF padrão. A [documentação do MAF](https://learn.microsoft.com/en-us/agent-framework/hosting/azure-functions) separa esse mecanismo da extensão Durable Task: checkpoints padrão retomam uma execução no runtime MAF; a extensão Durable Task acrescenta recuperação distribuída em workers duráveis e exige registrar os grafos que serão executados.

O MAF 1.22 já fornece `CheckpointManager.CreateJson(ICheckpointStore<JsonElement>)` e `InProcessExecution.ResumeAsync`; em contrapartida, `CheckpointManager.Default` é explicitamente in-memory e não prova retomada após reinício. O produto já usa `CheckpointManager.Default` em execuções colaborativas avançadas, então hoje esse caminho só mantém checkpoints enquanto o processo que contém o manager continua ativo.

Para definições criadas por tenant, `ICheckpointStore<JsonElement>` continua uma opção para persistir o estado interno de uma execução MAF específica. Não foi escolhido como scheduler do produto: ainda seria necessário guardar `tenantId`, `workflowId`, hash imutável da definição/agentes, descobrir trabalho após crash, coordenar leases entre nós e expor o mesmo execution ID/status usado pela UI. Implementar esse checkpoint isoladamente duplicaria o estado do `IWorkflowStore` sem corrigir start/poll/approval/cancel; avaliar somente como detalhe interno do runner após o fluxo canônico app-owned estar recuperável.

Há ainda um contrato de polling desalinhado a resolver: `DurableWorkflowCompiler` retorna `RunId` e promete consulta via `GET /api/workflow/executions/{id}`, mas `WorkflowController` consulta `IWorkflowEngine`/`IWorkflowStore`, não o client/estado DurableTask. Não apresentar o `RunId` como execução acompanhável até conectar ambos os lados e validar estados terminal, cancelamento e isolamento.

O registry de `DurableWorkflowOptions` vazio e o agendamento por nome confirmam que `IWorkflowClient.RunAsync(Workflow, ...)` não executa o grafo recebido dinamicamente. Esse caminho fica fora da arquitetura canônica do produto; a tool de Banner deve iniciar pela mesma aplicação de workflow e usar o mesmo ID/status que a UI consulta.

### Runtime e contrato de execução usados pela aplicação

O backend tem dois caminhos distintos que hoje são descritos como se fossem um só:

- `POST /api/workflow/executions/start/{id}` usa `DefaultWorkflowEngine`, que interpreta a definição tenant-scoped e grava execução/etapas em `IWorkflowStore`; o `GET /api/workflow/executions/{id}` consulta esse mesmo store. A definição executada não fica fixada por versão/hash e o start usa `Task.Run` no processo; não há worker de recuperação/lease que retome uma execução `Running` após crash.
- `DurableWorkflowCompiler`, usado pela `BannerProductionTool` quando `StorageMode=PostgreSQL`, monta um grafo MAF por request e agenda com `IWorkflowClient`. Retorna um RunId e orienta polling pelo endpoint acima, mas esse controller consulta o engine customizado, não o estado DurableTask. Portanto nem o start/poll é ponta a ponta, além do registry vazio e do worker builder ausente.
- `AgentCollaborationWorkflow` já usa `InProcessExecution` e checkpoints MAF quando habilitado, mas passa `CheckpointManager.Default`, documentado no pacote 1.22 como armazenamento in-memory. Isso serve à retomada dentro do processo, não a restart distribuído.

### Decisão de arquitetura para workflows editáveis por tenant

Usar `IWorkflowEngine`/`IWorkflowStore` como orquestrador canônico dos workflows que a plataforma permite criar/editar por tenant. Essa é a rota que o frontend realmente inicia, consulta e cancela; ela já interpreta definições dinâmicas e grava execução/etapas. O MAF permanece como runtime dos agentes e das ferramentas autorizadas dentro de cada etapa. Não usar `Microsoft.Agents.AI.DurableTask` para executar esses grafos arbitrários: o worker atual exige workflow registrado no startup, o provider PostgreSQL não tem builder compatível e a compilação por request não é transmitida ao worker.

O `DurableWorkflowCompiler` não pode anunciar um RunId consultável por `WorkflowController` enquanto grava/agenda em outro sistema. Incorporar a tool `BannerProductionTool` ao mesmo `IWorkflowEngine` e manter execução, status, cancelamento e eventos no mesmo `WorkflowExecution.Id`. O checkpoint JSON do MAF pode ser adotado dentro de uma etapa MAF depois de validar serialization/rehydration; ele não substitui scheduler, lease, estado de execução ou recuperação do produto.

| Etapa | Resultado verificável | Estado |
|---|---|---|
| Unificar o start da tool de Banner com o engine usado por `WorkflowController` | O ID retornado existe no `IWorkflowStore`; `GET`, cancelamento e SignalR consultam esse mesmo ID. Etapas `Agent` invocam o agente MAF e não são marcadas como sucesso vazias. | Pendente; `WorkflowController` agora também retorna `id` e mantém `executionId` |
| Fixar a definição executada | Cada execução persiste versão/hash e snapshot imutável da definição; retomada não lê uma versão editada após o start. | Implementado no modelo/engine/PostgreSQL; migration `20260929120423_AddWorkflowExecutionDefinitionSnapshot`. Testes unitário e PostgreSQL provaram retomada da versão 7 após editar o registro vivo para versão 8; hash semântico tolera a normalização de JSONB e rejeita adulteração. |
| Recuperar execução após falha | Runner hospedado reivindica execuções no PostgreSQL com lease/claim atômico; segundo nó não executa o mesmo claim enquanto a lease está válida; lease expirada pode ser recuperada. | Pendente; `DefaultWorkflowEngine` ainda usa `Task.Run` no processo |
| Definir semântica dos passos externos | Tool/agent têm chave idempotente por execução/etapa e retry explícito; aprovação pausa até approve/reject autorizado; wait é persistido; subworkflow é executado ou rejeitado na validação, nunca reportado como sucesso ignorando-o. | Parcial: `Agent`, approve/reject e atraso de `Wait` implementados; subworkflow falha explicitamente. Lease, persistência de Wait, idempotência e autorização granular de aprovador pendem |
| Verificar o contrato completo | Compose isolado: start/status/approval/cancel, dois workers concorrentes, restart de worker, retomada de etapa e isolamento tenant. Mesmo `executionId` e wire contract usado pelo frontend. | Testes Release atuais: 738 aprovados/1 skip; controller/approval, steps e snapshot versionado foram cobertos. Dois workers/restart ainda pendentes |

Semântica de efeitos externos será at-least-once: lease expirada pode repetir uma etapa cujo efeito ocorreu antes de o resultado ser persistido. Ferramentas com efeito externo precisam deduplicar pela chave idempotente ou oferecer compensação; o sistema não promete exactly-once. Reconsiderar DurableTask somente se um worker genérico MAF suportar a mesma definição versionada dinâmica, status/poll e integração PostgreSQL compatível.

### Impacto previsto no frontend

O backend agora retorna `id` e mantém `executionId` no `202`, porque `workflowApi.startWorkflow` é tipado como `WorkflowExecution` e o editor seleciona `execution.id`. Os endpoints `approve`/`reject` já usados por `workflowApi` passam a existir. Ainda há dois pontos para um follow-up do frontend: `useWorkflowExecution` só refaz polling quando status é `0` (Pending), embora execução Running seja `1`; e o hook não escuta `ApprovalRequested`, então a transição para aprovação pode não aparecer se a leitura inicial ocorrer antes. O backend mantém o evento/contrato enquanto o ajuste do hook é planejado; nenhuma alteração de frontend pertence a esta etapa.

MAF tem breaking changes após 1.9.0; hosting A2A/AG-UI continua preview e sua validação E2E não é gate desta issue. O pacote Microsoft.Agents.AI.DurableTask está na linha 1.16 preview sem publicação 1.22. O provider DurableTask PostgreSQL instalado descreve suporte PostgreSQL 17+, enquanto o Compose de validação usa PostgreSQL 16; portanto ele não serve para alegar compatibilidade do provider. Há também incompatibilidade funcional: o client agenda por nome de workflow e o worker constrói registry a partir das definições registradas no startup; os grafos dinâmicos por tenant/request não entram nesse registry, e o pacote PostgreSQL não fornece o worker builder para os tipos Durable Task atuais. A decisão para grafos editáveis por tenant é usar `IWorkflowEngine` e seu store como scheduler/estado canônico; a extensão DurableTask não será usada para executar essas definições arbitrárias. A validação de configuração global e persistência pode usar o Compose isolado de PostgreSQL 16. No pacote MAF 1.16.0-preview.260922.1, o limite de supersteps (100 por default) é configurável via `DurableWorkflowOptions.MaxSupersteps` e o runner lança `MaxSuperstepsExceededException` quando há trabalho restante; a configuração do backend expõe `AgenticSystem:LocalExecution:DurableWorkflowMaxSupersteps` com default 100. O provider global é mutável em singleton, mas sua persistência atual passa pela configuração tenant-scoped; isso conflita com gestão por Platform Admin, restart e múltiplas instâncias. Separar configuração global de plataforma e BYOK por tenant, sem reutilizar o mesmo registro. O Gateway contabiliza custo estimado fixo por chamada, separado da quota persistida de custo por tenant. Se provider Ollama real não estiver disponível, validar runtime com stub e registrar explicitamente que a evidência E2E real ficou pendente.

## Entrega

Commits separados por contexto (rastreabilidade, atualização MAF, Gateway/protocolos, evidências); staging alheio preservado. Criar/atualizar PR com resultado e limitações, mantê-lo draft se houver lacuna funcional ou gate obrigatório pendente. Não fazer merge/deploy.
