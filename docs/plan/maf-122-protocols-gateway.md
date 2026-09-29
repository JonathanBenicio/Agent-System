# Plano — Atualizar MAF e integrar providers ao Gateway

Status: implementação MAF/Gateway concluída e validada; worker recuperou Wait após encerramento forçado da API. Restam a geração funcional de Banner no engine canônico, a deduplicação de efeitos externos e a publicação das evidências em #120. [Evidência de runtime](../backend/validation/maf-122-workflow-runtime-2026-09-29.md) · Issue: [#120](https://github.com/JonathanBenicio/Agent-System/issues/120) · [ADR-036](../architecture/adr/036-maf-122-protocols-and-gateway.md) · Story: BACK-MAF-120.

Baseline: `f941198` · 2026-09-29 · Branch: `fix/backend-core-tenancy`. Matriz de breaking changes MAF 1.9 para 1.22 concluída antes do bump; supervisor dinâmico implementado separadamente em [dynamic-orchestrator-implementation.md](dynamic-orchestrator-implementation.md). Compose PostgreSQL/Ollama isolado usado; A2A/AG-UI E2E permanece despriorizado em #121 por ser preview. `.gitignore` staged e arquivos pessoais foram preservados.

Análise de breaking changes e matriz das APIs do repositório: [maf-122-compatibility-review.md](maf-122-compatibility-review.md). Concluída em 2026-09-29 antes do bump; restore/build Release com versões de destino passam.

## Objetivo e escopo

Atualizar os pacotes MAF efetivamente usados após revisar mudanças incompatíveis; preservar o contrato tenant-scoped de sessão; registrar e encaminhar providers globais pelo Gateway em produção. A implementação funcional do supervisor e a consolidação das sessões de especialistas ficam no plano separado do orquestrador. Para A2A/AG-UI, somente compatibilidade/build; E2E em #121. Sem merge, deploy, mudança no frontend ou redução do gate de cobertura.

## Etapas

| Entrega | Dependência | Verificação | Estado/evidência |
|---|---|---|---|
| Issue, ADR, story e rastreabilidade | Nenhuma | Links cruzados e índices sincronizados | #120 continua aberto; o corpo público ainda precisa receber 753/1 e as novas evidências. O conector GitHub não está disponível nesta sessão. #121 (A2A/AG-UI preview) permanece separado/despriorizado. |
| Matriz de compatibilidade 1.9.0 para 1.22.0 | Issue/ADR/story | APIs comparadas às releases oficiais; dependências NuGet registradas | Concluída antes do bump em `maf-122-compatibility-review.md`. |
| Atualização coordenada dos pacotes e sessão | Matriz concluída | Restore/build Release, partições de sessão, migrations | MAF 1.22 e build limpo. Sessões MAF persistidas/reabertas no PostgreSQL, tenant cruzado negado e sessão do supervisor reaberta após reinício real da API. Suíte: 753 aprovados, 1 skip vetorial, 0 falhas. |
| Compatibilidade de hosting A2A/AG-UI | Build atualizado | Mapeamento com `ValidateScopes=true`, sem alegar E2E | 2 testes de registro passaram; E2E em #121 |
| Registry e caminho de execução Gateway | MAF atualizado | Startup/runtime, resposta/stream, falha/cancelamento/fallback e BYOK | DI PostgreSQL inicia o hosted service de produção. Teste com dois graphos independentes de DI/LLMManager/Gateway recebeu PostgreSQL NOTIFY, habilitou o provider em ambos e fez inferência real qwen2.5:0.5b por cada Gateway; ambos registraram request saudável. Wrapper streaming/falha/cancelamento está coberto com client fake; refresh entre processos diferentes não foi testado. |
| Separar configuração global de provider e BYOK | Decisão Platform Admin | Persistência global cifrada/auditada; BYOK isolado; reload em instâncias independentes | Implementado/validado no PostgreSQL: store global sem `TenantId`, segredo cifrado, auditoria por hash, NOTIFY, dois DI graphs independentes de LLMManager/Gateway. Ambos receberam enabled/model atualizado e fizeram inferência real Ollama saudável. Instâncias na mesma máquina de teste; processo/host reiniciado não testado. |
| Verificação final | Etapas anteriores | Build, suíte PostgreSQL/Ollama, EF e links | Build Release 0 avisos/erros; suíte PostgreSQL/Ollama: 753 aprovados, 1 teste vetorial ignorado, 0 falhas; EF sem mudanças pendentes no último exame. Migrations de configuração/lease/Wait aplicadas. API reiniciada após sessão MAF e Wait; efeito externo interrompido não exercitado. |

## Limite com o plano do orquestrador

O upgrade MAF/Gateway e o supervisor dinâmico estão implementados. O fechamento da entrega de workflow depende de resolver ou separar explicitamente a regressão da geração de Banner; start/status e a geração efetiva são critérios distintos.

## Critérios de aceite

- [x] Pacotes e APIs atualizados para o conjunto compatível MAF 1.22.0; restore e build Release passaram sem avisos/erros.
- [x] Sessões MAF mantêm partições, serialização, owner/tenant e retomada com novos adapters/contextos PostgreSQL; tenant cruzado é negado. A API reabriu a sessão persistida do supervisor após reinício real.
- [x] Hosting A2A/AG-UI compila e os registros passam com validação de escopos; E2E de autenticação/isolamento/streaming fica despriorizado em #121 enquanto os hosts seguem preview.
- [x] Providers habilitados no host são registrados e chamados via Gateway; dois managers/Gateways independentes receberam reload PostgreSQL e cada um fez inferência real Ollama. Wrapper de streaming e falhas/cancelamento do Gateway também foram exercitados. BYOK mantém rota/quota tenant-scoped e providers desabilitados não são registrados.
- [x] Falha/cancelamento/stream atualizam health/failure counters nos testes Gateway; quotas PostgreSQL permanecem aplicadas por tenant.
- [x] Só Platform Admin pode alterar configuração global de provider; credenciais BYOK permanecem tenant-scoped, coberto por testes de controller/autorização.
- [x] Configuração global usa store sem `TenantId`, segredos cifrados, auditoria por hash e PostgreSQL NOTIFY; migration aplicada e leitura entre tenants testada.
- [x] Store global separa configuração de provider dos segredos BYOK; API não retorna segredo em texto puro e auditoria não guarda segredo.
- [x] Precedência por request preserva sessão/credencial explícita, BYOK tenant e configuração global; BYOK não usa circuit/rate state global.
- [x] Migration global explícita foi aplicada sem promover configuração tenant-scoped; build, suíte PostgreSQL/Ollama e EF pending-model passaram.

## Validação

Usar testes unitários para regressões do contrato e host/harness integrado para protocolos/Gateway. Testes PostgreSQL devem apontar ambas as variáveis somente para `127.0.0.1:55432/backend_validation/validation` do Compose isolado; usar `-p` específico por execução para separar containers/volumes e conferir o mapeamento de porta antes de aplicar migrations. Registrar comandos, versão/ambiente, aprovação, falha, skip e não execução separadamente. Cobertura é reportada como métrica e gate existente, não como objetivo principal da entrega.

## Riscos e gaps

A linha instalada de `Microsoft.Agents.AI.DurableTask` é preview, não compatível com os workflows arbitrários por tenant e não é usada pela rota canônica. A recuperação é fornecida pelo worker PostgreSQL do `IWorkflowStore`; Wait completou após encerramento forçado da API antes do prazo. Faltam prova de interrupção durante efeito externo e garantias de idempotência do handler. O registry de DurableTask não resolve esse caso de uso dinâmico.

### Limite do checkpoint MAF para workflows dinâmicos

Os samples .NET oficiais [CheckpointAndRehydrate](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Checkpoint/CheckpointAndRehydrate) e [AotCheckpointing](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Declarative/AotCheckpointing) mostram o modelo de checkpoint/rehydration do runtime MAF padrão. A [documentação do MAF](https://learn.microsoft.com/en-us/agent-framework/hosting/azure-functions) separa esse mecanismo da extensão Durable Task: checkpoints padrão retomam uma execução no runtime MAF; a extensão Durable Task acrescenta recuperação distribuída em workers duráveis e exige registrar os grafos que serão executados.

O MAF 1.22 já fornece `CheckpointManager.CreateJson(ICheckpointStore<JsonElement>)` e `InProcessExecution.ResumeAsync`; em contrapartida, `CheckpointManager.Default` é explicitamente in-memory e não prova retomada após reinício. O produto já usa `CheckpointManager.Default` em execuções colaborativas avançadas, então hoje esse caminho só mantém checkpoints enquanto o processo que contém o manager continua ativo.

Para definições criadas por tenant, `ICheckpointStore<JsonElement>` continua uma opção para persistir o estado interno de uma execução MAF específica. Não foi escolhido como scheduler do produto: ainda seria necessário guardar `tenantId`, `workflowId`, hash imutável da definição/agentes, descobrir trabalho após crash, coordenar leases entre nós e expor o mesmo execution ID/status usado pela UI. Implementar esse checkpoint isoladamente duplicaria o estado do `IWorkflowStore` sem corrigir start/poll/approval/cancel; avaliar somente como detalhe interno do runner após o fluxo canônico app-owned estar recuperável.

Banner não usa mais o client DurableTask separado: o start, status, cancelamento e eventos usam o `IWorkflowEngine` canônico e o mesmo ID persistido. A definição semeada ainda exige `PromptTemplate`, `ModelOverride`, `AllowedToolsOverride` e entrada multimodal, que o engine atual não aplica. Assim, start/status não prova geração do Banner; essa lacuna funcional impede declarar a tool concluída.

A incompatibilidade do registry estático e do scheduling por nome motivou retirar os compiladores DurableTask da rota dinâmica. A tool de Banner passou a iniciar pelo engine canônico e a retornar o ID pollable do produto; `DurableTask` permanece fora dessa arquitetura.

### Runtime e contrato de execução usados pela aplicação

O mecanismo canônico de execução para workflows dinâmicos é o engine da aplicação; os checkpoints MAF usados em outros fluxos colaborativos continuam sendo um runtime in-process separado.

- `POST /api/workflow/executions/start/{id}` usa `DefaultWorkflowEngine`, que interpreta a definição tenant-scoped e grava execução/etapas em `IWorkflowStore`; `GET /api/workflow/executions/{id}` consulta esse mesmo store. O start persiste a execução `Pending`; um worker hospedado reivindica com lease/heartbeat, fixa a execução ao snapshot/version/hash salvo e permite recovery após expiração do lease. Teste PostgreSQL disputa simultaneamente o claim entre dois workers e valida que somente um recebe a execução; depois simula expiração e comprova fencing do worker anterior. Restart abrupto enquanto uma etapa externa está em andamento ainda não foi exercitado.
- `BannerProductionTool` procura uma definição chamada `Banner Production Workflow` somente na lista do tenant ativo, inicia pelo `IWorkflowEngine` e retorna o mesmo `executionId` que o controller consulta. Testes PostgreSQL confirmam ID/status no mesmo store, leitura cruzada negada e recusa de colisão de ID global entre tenants. A rota `IDynamicWorkflowCompiler` e seus compiladores desconectados foram removidos.
- `AgentCollaborationWorkflow` já usa `InProcessExecution` e checkpoints MAF quando habilitado, mas passa `CheckpointManager.Default`, documentado no pacote 1.22 como armazenamento in-memory. Isso serve à retomada dentro do processo, não a restart distribuído.

### Decisão de arquitetura para workflows editáveis por tenant

Usar `IWorkflowEngine`/`IWorkflowStore` como orquestrador canônico dos workflows que a plataforma permite criar/editar por tenant. Essa é a rota que o frontend realmente inicia, consulta e cancela; ela já interpreta definições dinâmicas e grava execução/etapas. O MAF permanece como runtime dos agentes e das ferramentas autorizadas dentro de cada etapa. Não usar `Microsoft.Agents.AI.DurableTask` para executar esses grafos arbitrários: o worker atual exige workflow registrado no startup, o provider PostgreSQL não tem builder compatível e a compilação por request não é transmitida ao worker.

A rota de Banner inicia pela aplicação de workflow canônica e devolve `id`, `executionId` e `statusUrl` do mesmo `IWorkflowStore`; não publica RunId de outro runtime. Um teste PostgreSQL verificou persistência e isolamento por tenant.

| Etapa | Resultado verificável | Estado |
|---|---|---|
| Unificar o start da tool de Banner com o engine usado por `WorkflowController` | Mesmo ID persistido, consultável, cancelável e transmitido pelos eventos do engine; definição do tenant ativo | Start/status e isolamento implementados e validados; geração da imagem final permanece aberta porque o engine ignora recursos da definição semeada. |
| Fixar a definição executada | Cada execução persiste versão/hash e snapshot imutável da definição; retomada não lê uma versão editada após o start. | Implementado no modelo/engine/PostgreSQL; migration `20260929120423_AddWorkflowExecutionDefinitionSnapshot`. Testes unitário e PostgreSQL provaram retomada da versão 7 após editar o registro vivo para versão 8; hash semântico tolera a normalização de JSONB e rejeita adulteração. |
| Recuperar execução após falha | Claim/lease atômico entre workers e retomada após expiração | Implementado e validado em PostgreSQL: disputa simultânea fornece um único claim, lease expirada é recuperada por outro worker e o antigo não renova nem grava. Wait completou após encerramento forçado da API 59,5 s antes do prazo. Efeito externo interrompido segue não demonstrado. |
| Definir semântica dos passos externos | `MaxRetries`, autorização `Permission.Execute`, chave estável por execução/step, Wait persistido, Subworkflow explícito | Implementado e testado. `ToolInput.IdempotencyKey` é entregue igual em retries; handler externo precisa deduplicar e não há promessa exactly-once. Wait foi retomado somente após seu prazo. |
| Verificar o contrato completo | Compose isolado: start/status/approval/cancel, claims concorrentes, Wait retomado, Banner e isolamento tenant | Suíte: 753 aprovados/1 skip; PostgreSQL cobre claim concorrente, lease recovery/fencing, Wait após reinício real, start/status de Banner e proteção a colisão de ID tenant. Geração funcional de Banner e efeito externo interrompido continuam não validados. |

Semântica de efeitos externos será at-least-once: lease expirada pode repetir uma etapa cujo efeito ocorreu antes de o resultado ser persistido. Ferramentas com efeito externo precisam deduplicar pela chave idempotente ou oferecer compensação; o sistema não promete exactly-once. Reconsiderar DurableTask somente se um worker genérico MAF suportar a mesma definição versionada dinâmica, status/poll e integração PostgreSQL compatível.

### Impacto previsto no frontend

O backend agora retorna `id` e mantém `executionId` no `202`, porque `workflowApi.startWorkflow` é tipado como `WorkflowExecution` e o editor seleciona `execution.id`. Os endpoints `approve`/`reject` já usados por `workflowApi` passam a existir. Ainda há dois pontos para um follow-up do frontend: `useWorkflowExecution` só refaz polling quando status é `0` (Pending), embora execução Running seja `1`; e o hook não escuta `ApprovalRequested`, então a transição para aprovação pode não aparecer se a leitura inicial ocorrer antes. O backend mantém o evento/contrato enquanto o ajuste do hook é planejado; nenhuma alteração de frontend pertence a esta etapa.

O hosting A2A/AG-UI continua preview e sua validação E2E está separada em #121. O pacote DurableTask da linha 1.16 preview usa registro estático no worker e não executa os grafos arbitrários por tenant; o provider PostgreSQL DurableTask requer PostgreSQL 17+, enquanto este Compose usa PostgreSQL 16. O engine da aplicação é o scheduler canônico dessas definições. A configuração global de providers foi separada da BYOK tenant-scoped e testada com PostgreSQL NOTIFY e Ollama real. O Gateway contabiliza custo estimado fixo por chamada, separado da quota persistida de custo por tenant.

## Entrega

Commits separados por contexto (rastreabilidade, atualização MAF, Gateway/protocolos, evidências); staging alheio preservado. Criar/atualizar PR com resultado e limitações, mantê-lo draft se houver lacuna funcional ou gate obrigatório pendente. Não fazer merge/deploy.
