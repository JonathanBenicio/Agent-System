# DI Runtime Audit

Data: 2026-05-07

## Escopo

Auditoria estatica dos registros de DI em:

- src/AgenticSystem.Core/Extensions/ServiceCollectionExtensions.cs
- src/AgenticSystem.Infrastructure/Extensions/ServiceCollectionExtensions.cs
- src/AgenticSystem.Api/Program.cs

Objetivo:

- separar o que participa do fluxo principal
- identificar superficies secundarias
- listar registros sem resolucao visivel no codigo
- propor uma limpeza segura

## Fluxo Principal Revalidado

Fluxo principal do chat automatico:

1. ChatHub
2. MetaAgentOrchestrator
3. AgentExecutionWorkflow
4. FrameworkOrchestratorService
5. hosted AIAgent do orquestrador

Esse caminho continua sendo a trilha principal do produto.

## Core do Runtime

Servicos e wrappers que continuam no caminho principal ou em suas dependencias diretas:

- IMetaAgent / MetaAgentOrchestrator
- IAgentExecutionWorkflow / AgentExecutionWorkflow
- IFrameworkOrchestratorService / FrameworkOrchestratorService
- IAgentFactory / HierarchicalAgentFactory
- ISessionManager / SessionManager
- IAgentRuntimeCoordinator / AgentRuntimeCoordinator
- ILLMRuntimeContextAccessor / LLMRuntimeContextAccessor
- IChatClient com ContextAwareChatClient + GovernedChatClient
- IAgentExecutionPreProcessingPipeline / AgentExecutionPreProcessingPipeline
- IAgentExecutionPostProcessingPipeline / AgentExecutionPostProcessingPipeline
- ICorrectionLoop / CorrectionLoopService
- IFinalResponseApprovalService / FinalResponseApprovalService
- IRAGService / RAGService
- ISmartRouter / SmartRouter
- IContextAnalyzer / ContextAnalyzer
- IContextBudgetManager / ContextBudgetManager
- IMCPPluginManager / MCPPluginManager
- McpToolsAIFunctionAdapter
- UnifiedAIToolProvider
- AgentFrameworkFactory
- SimpleSessionStoreAdapter
- OrchestratorAuxiliaryToolService
- OrchestratorInstructionService
- OrchestratorToolBindingService
- RAGContextProvider
- OrchestratorHostBuilder

Observacao:

- OrchestratorContextFactory esta marcado como deprecated, mas ainda participa do hot path via DI para resolver OrchestratorContext. Nao e codigo morto.

## Superficies Secundarias

Registros com consumidor visivel, mas fora da jornada principal de chat automatico:

- ChatClientPlanner, usado por PlannerController
- ISetupFlowManager / SetupFlowManager, usado por SetupController
- IDirectAgentRequestExecutor / DirectAgentRequestExecutor, usado no caminho de chat direto com targetAgent
- IDirectAgentExecutionService / AgentFrameworkDirectExecutionService, usado apenas na execucao direta
- IScheduledTaskManager / ScheduledTaskManager e ITriggerEngine / TriggerEngine, usados por ScheduledTasksController e hosted service
- IConfigManager / ConfigManager, usado por ConfigController e por integracoes de configuracao
- IEmbeddingMigrationManager / EmbeddingMigrationManager, usado por EmbeddingMigrationController
- IDocumentIngestionPipeline / DocumentIngestionPipeline, usado por DocumentController
- IServiceGateway / ServiceGateway, usado por GatewayController e GatewayHub
- IMCPPluginManager / MCPPluginManager, tambem exposto por superficies de administracao MCP
- Protocol hosting de A2A e AG-UI, registrado em Program.cs como superficie opcional

Essas partes nao sao codigo morto, mas tambem nao compoem o stable core descrito nas diretrizes do repositorio.

## Registros Sem Resolucao Visivel

Pelo cruzamento estatico de tipos registrados versus ocorrencias em src, os candidatos mais fortes a orfao sao:

### Sem consumidor visivel fora do proprio contrato e implementacao

- IChunkLifecycleManager / ChunkLifecycleManager
- IDynamicAgentService / DynamicAgentService
- IToolAvailabilityGuard / ToolAvailabilityGuard

### Subgrafo sem entrada visivel no runtime atual

- IAgentCollaborationWorkflow / AgentCollaborationWorkflow
- IAgentChannelService / FrameworkAgentChannelService
- IToolDiscoveryService / ToolDiscoveryService

Notas:

- IToolDiscoveryService aparece apenas acoplado a ToolAvailabilityGuard, que tambem nao tem entrada visivel.
- IAgentChannelService aparece apenas acoplado ao fluxo de colaboracao.
- Nao foi encontrado AddWorkflow("collaboration") no runtime atual.

### Adapter registrado sem consumidor visivel fora do DI

- AgenticVectorStoreAdapter

Nota:

- O adapter esta registrado no container, mas nao apareceu nenhum consumidor visivel em src fora do proprio arquivo e do registro de DI.

## Fluxos Esperados dos Orfaos

Nem todo registro orfao parece erro. O cruzamento com documentacao, testes e contratos de produto indica tres situacoes diferentes.

### 1. Orfaos que parecem integracao interrompida

- IDynamicAgentService / DynamicAgentService
	- A documentacao viva e os testes descrevem um fluxo de criacao por linguagem natural dentro da orquestracao.
	- O runtime atual nao chama esse servico no chat nem no controller de criacao; o AgentController usa IAgentFactory diretamente.
	- Se a capacidade ML11 continuar valida, esse servico deveria voltar em um fluxo explicito de criacao dinamica por chat ou ser rebaixado para lab.

- IToolAvailabilityGuard / ToolAvailabilityGuard
- IToolDiscoveryService / ToolDiscoveryService
	- Os docs descrevem um passo de validacao entre analise e execucao, com sugestoes de MCP/plugin quando faltam tools.
	- O ConfidenceScoreCalculator ainda tem suporte a cobertura de tools, mas o post-processing hoje passa toolAvailability = null.
	- Isso sugere uma integracao pela metade: ou o guard deve voltar ao pipeline, ou o desenho precisa ser oficialmente removido da documentacao viva.

### 2. Orfaos que parecem feature de lab ou rollout incompleto

- IAgentCollaborationWorkflow / AgentCollaborationWorkflow
- IAgentChannelService / FrameworkAgentChannelService
	- Ha implementacao robusta, testes dedicados, opcoes em appsettings e documentacao de workflow planner -> executor -> reviewer.
	- Mesmo assim, nao ha chamador visivel no runtime atual nem AddWorkflow("collaboration") em src.
	- O sinal mais forte aqui nao e dead code puro, mas um slice experimental que deveria estar atras de feature flag e de um ponto de entrada explicito, em vez de permanecer registrado como se participasse do fluxo principal.

### 3. Orfaos que hoje nao tem fluxo obrigatorio claro

- AgenticVectorStoreAdapter
	- A documentacao mostra o adapter como bridge para interoperabilidade com Microsoft.Extensions.VectorData.
	- Os testes cobrem a classe, mas o runtime atual continua consumindo IVectorStore diretamente em RAG, ingestao, migracao e sync.
	- Nao ha evidencia de um fluxo de produto quebrado; hoje parece um adapter pronto para uso futuro, mas sem consumidor concreto.

- IChunkLifecycleManager / ChunkLifecycleManager
	- A capacidade existe em user stories e no mapa de maturity levels, mas nao aparece ligada a um fluxo operacional atual.
	- Diferente de DynamicAgentService e ToolAvailabilityGuard, aqui nao apareceu nenhum contrato vivo dizendo exatamente onde a invocacao deveria acontecer hoje.
	- O caso parece mais de feature registrada cedo demais do que regressao de wiring.

## Leitura Recomendada

Quando um orfao vier de documentacao historica do fluxo imperativo, nao tente religar esse servico no AgentExecutionWorkflow antigo. No runtime atual, qualquer reintroducao precisa acontecer ao redor de:

- FrameworkOrchestratorService
- pipelines de pre/post-processing
- tools auxiliares do orquestrador
- ou um endpoint/feature flag explicito para lab zone

## Registros com Resolucao Indireta pelo Framework

Esses casos tem pouca visibilidade no codigo, mas nao devem ser tratados como orfaos:

- ScheduledTaskHostedService
- AgentCleanupHostedService
- DynamicSkillCatalogHostedService
- ContextAwareChatClient

Motivo:

- hosted services sao resolvidos pelo host
- ContextAwareChatClient e resolvido na fabrica do IChatClient

## Proposta de Limpeza Segura

### Fase 1 - Sem mudar comportamento do produto

- remover o registro de AgenticVectorStoreAdapter
- mover IAgentCollaborationWorkflow e IAgentChannelService para registro condicional por feature flag
- mover IDynamicAgentService para feature flag ou remover o registro se a funcionalidade nao tiver rollout previsto
- mover IToolAvailabilityGuard e IToolDiscoveryService para feature flag ou remover o registro junto
- remover IChunkLifecycleManager do DI se nao houver plano imediato de uso

### Fase 2 - Reducao de divida tecnica do hot path

- substituir o uso de OrchestratorContextFactory por resolucao direta via OrchestratorHostBuilder
- depois remover OrchestratorContextFactory

### Fase 3 - Promocao ou exclusao explicita das superficies laterais

- manter apenas o que tem endpoint, hub ou contrato externo ativo
- o restante deve ir para lab zone por default

## Separacao Recomendada: Core vs Lab

### Core

- chat automatico principal
- sessao e streaming
- orquestracao framework-first
- RAG minimo
- tools MCP necessarias ao runtime
- quality gates minimos
- observabilidade minima

### Lab zone ou feature flag

- collaboration workflow
- chat dedicado por agente como experiencia paralela
- dynamic agent service
- tool discovery e availability guard
- setup flow, se continuar como onboarding opcional e nao jornada padrao
- surfaces administrativas e workflows especializados

## Conclusao

O runtime atual esta mais enxuto do que o DI sugere. O principal excesso esta em clusters registrados sem entrada visivel no fluxo principal, especialmente colaboracao, agentes dinamicos, discovery/guard de tools e o adapter AgenticVectorStoreAdapter.