# Schemas dos contratos MVC

Gerado do [OpenAPI Release](openapi-release.json) pelas registrations MVC/Swagger da API nesta branch de integração. Obrigatoriedade/binding reflete o OpenAPI; validações de negócio no controller/store podem ser mais restritas. Consulte [núcleo](api-core.md), [inventário](endpoint-inventory.md) e fontes. Swagger declara segurança global ApiKey, mas o runtime aceita também JWT e há actions com auth própria: use [guia de acesso](access-tenants.md), sem inferir política só pelo spec. Este schema não abrange hubs ou protocolos de bibliotecas.

### AgentSpecification

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | não |
| description | string | não |
| tier | AgentTier | não |
| domain | string | não |
| allowedTools | array of string | não |
| capabilities | array of string | não |
| autonomyLevel | AutonomyLevel | não |
| policyIds | array of string | não |
| instructions | string | não |
| configuration | object | não |
| workflowTemplate | string | não |
| autoCleanupAfter | string | não |

### AgentTier

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### ApprovalDecisionRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| decidedBy | string | não |
| comment | string | não |

### AssignTenantMembershipRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| role | string | não |

### AutonomyLevel

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### BrainstormRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| description | string | não |

### ChannelInfo

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | não |
| healthy | boolean | não |

### ChatCompletionChoice

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| index | integer | não |
| message | ChatCompletionMessage | não |
| finish_reason | string | não |

### ChatCompletionError

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| error | ChatCompletionErrorDetail | não |

### ChatCompletionErrorDetail

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| message | string | não |
| type | string | não |
| param | string | não |
| code | string | não |

### ChatCompletionMessage

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| role | string | não |
| content | string | não |
| name | string | não |

### ChatCompletionRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| model | string | não |
| messages | array of ChatCompletionMessage | não |
| temperature | number | não |
| max_tokens | integer | não |
| stream | boolean | não |
| user | string | não |

### ChatCompletionResponse

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| object | string | não |
| created | integer | não |
| model | string | não |
| choices | array of ChatCompletionChoice | não |
| usage | ChatCompletionUsage | não |
| system_fingerprint | string | não |

### ChatCompletionUsage

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| prompt_tokens | integer | não |
| completion_tokens | integer | não |
| total_tokens | integer | não |

### ChatRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| message | string | não |
| userId | string | não |
| userName | string | não |
| targetAgent | string | não |
| provider | string | não |
| model | string | não |
| apiKey | string | não |
| context | object | não |
| sessionId | string | não |

### ConditionType

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### ConfigCategory

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### ConfigEntryRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| key | string | não |
| value | string | não |
| isSecret | boolean | não |
| category | ConfigCategory | não |
| description | string | não |
| provider | string | não |
| expiresAt | string | não |

### CreateGoldenSetDto

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | sim |
| description | string | não |
| agentName | string | sim |
| cases | array of GoldenSetCaseDto | sim |

### CreatePlanRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| userId | string | não |
| objective | string | não |

### CreateSkillRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| name | string | não |
| domain | string | não |
| type | string | não |
| systemPromptFragment | string | não |
| fewShotExamples | string | não |
| metadata | object | não |

### CreateSupportGrantRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| userId | string | não |
| reason | string | não |
| expiresAt | string | não |

### CreateTaskRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | não |
| cronExpression | string | não |
| intervalSeconds | integer | não |
| maxRetryAttempts | integer | não |
| associatedRule | TriggerRule | não |

### DeliveryResult

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| channelName | string | não |
| status | DeliveryStatus | não |
| attempts | integer | não |
| deliveredAt | string | não |
| errorMessage | string | não |
| httpStatusCode | integer | não |

### DeliveryStatus

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### DiscoverModelsRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| apiKey | string | não |

### EmbeddingModelConfig

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| name | string | não |
| provider | EmbeddingProvider | não |
| modelName | string | não |
| dimensions | integer | não |
| apiKey | string | não |
| baseUrl | string | não |
| isActive | boolean | não |
| createdAt | string | não |

### EmbeddingProvider

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### FidesTenantPolicy

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| tenantId | string | não |
| enabledDetectors | object | não |
| version | integer | não |
| updatedBy | string | não |
| updatedAt | string | não |

### GatewaySettings

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| defaultDailyBudget | number | não |
| defaultFailureThreshold | integer | não |
| defaultBreakDurationSeconds | integer | não |
| defaultRequestsPerMinute | integer | não |

### GoldenSetCaseDto

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| input | string | não |
| expectedOutput | string | não |
| tags | array of string | não |

### HotSwapRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| subsystem | string | não |

### ImprovementType

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### InboundWebhookEntity

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| tenantId | string | não |
| name | string | não |
| secret | string | não |
| targetWorkflowId | string | não |
| targetAgentName | string | não |
| isActive | boolean | não |
| createdAt | string | não |
| lastTriggeredAt | string | não |

### KnowledgeRoom

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| name | string | não |
| description | string | não |
| color | string | não |
| icon | string | não |
| documentCount | integer | não |
| tags | array of string | não |
| createdAt | string | não |
| updatedAt | string | não |

### KnowledgeRoomPermissionRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| userId | string | não |
| role | KnowledgeRoomRole | não |

### KnowledgeRoomRole

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### LoginRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| apiKey | string | não |

### MCPPluginConfig

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| name | string | não |
| description | string | não |
| transportType | MCPTransportType | não |
| command | string | não |
| arguments | array of string | não |
| workingDirectory | string | não |
| environmentVariables | object | não |
| endpoint | string | não |
| headers | object | não |
| autoStart | boolean | não |
| createdAt | string | não |

### MCPTransportType

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### MemorySettings

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| obsidianVaultPath | string | não |
| vectorStoreType | string | não |
| connectionString | string | não |
| qdrant | QdrantSettings | não |
| pinecone | PineconeSettings | não |

### PineconeSettings

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| apiKey | string | não |
| environment | string | não |
| host | string | não |
| indexName | string | não |
| namespace | string | não |

### ProblemDetails

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| type | string | não |
| title | string | não |
| status | integer | não |
| detail | string | não |
| instance | string | não |

### ProcessStepRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| userId | string | não |
| response | string | não |

### QdrantSettings

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| url | string | não |
| apiKey | string | não |

### RegisterApiKeyRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | não |
| apiKey | string | não |
| isDefault | boolean | não |

### ScheduledTask

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| name | string | não |
| schedule | string | não |
| interval | string | não |
| status | ScheduledTaskStatus | não |
| maxRetryAttempts | integer | não |
| createdAt | string | não |
| nextRunAt | string | não |
| lastRunAt | string | não |
| lastFailedAt | string | não |
| totalExecutions | integer | não |
| failedExecutions | integer | não |
| consecutiveFailures | integer | não |
| deadLetterReason | string | não |
| associatedRule | TriggerRule | não |
| timeZoneId | string | não |
| dependencyTaskIds | array of string | não |
| continuationTaskIds | array of string | não |

### ScheduledTaskStatus

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### ScheduledTasksHealthReport

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| totalTasks | integer | não |
| activeTasks | integer | não |
| pausedTasks | integer | não |
| failedTasks | integer | não |
| totalRules | integer | não |
| enabledRules | integer | não |
| channels | array of ChannelInfo | não |
| overallHealthy | boolean | não |

### SelfImprovementRecord

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| tenantId | string | não |
| agentName | string | não |
| type | ImprovementType | não |
| originalBehavior | string | não |
| improvedBehavior | string | não |
| trigger | string | não |
| confidenceGain | number | não |
| confidenceLevel | number | não |
| learnedAt | string | não |
| applied | boolean | não |
| status | string | não |
| rationale | string | não |
| createdBy | string | não |
| reviewedBy | string | não |
| reviewedAt | string | não |
| previousInstructions | string | não |
| appliedPromptVersion | integer | não |
| appliedAgentVersionId | string | não |
| proposedChanges | object | não |

### SetSkillEnabledRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| enabled | boolean | não |

### StartMigrationRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| sourceModelId | string | não |
| targetModelId | string | não |
| sourceCollection | string | não |
| autoSwitch | boolean | não |
| deleteSourceAfterCompletion | boolean | não |

### StartSetupRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| userId | string | não |

### TaskExecution

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| executionId | string | não |
| taskId | string | não |
| attemptNumber | integer | não |
| startedAt | string | não |
| completedAt | string | não |
| success | boolean | não |
| deadLettered | boolean | não |
| errorMessage | string | não |
| duration | string | não |

### ToolInput

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| action | string | não |
| parameters | object | não |
| userId | string | não |
| idempotencyKey | string | não |

### TriggerAction

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| actionType | string | não |
| description | string | não |
| parameters | object | não |

### TriggerCondition

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| type | ConditionType | não |
| expression | string | não |
| expectedValue | string | não |

### TriggerEvaluationResult

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| ruleId | string | não |
| ruleName | string | não |
| conditionMet | boolean | não |
| actualValue | string | não |
| expectedValue | string | não |
| evaluatedAt | string | não |
| errorMessage | string | não |

### TriggerRule

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| name | string | não |
| description | string | não |
| schedule | string | não |
| source | TriggerSource | não |
| condition | TriggerCondition | não |
| action | TriggerAction | não |
| deliveryChannels | array of string | não |
| enabled | boolean | não |
| createdAt | string | não |
| lastTriggeredAt | string | não |
| executionCount | integer | não |
| timeZoneId | string | não |

### TriggerSource

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| type | TriggerSourceType | não |
| endpoint | string | não |
| headers | object | não |
| body | string | não |

### TriggerSourceType

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### UpdateApiKeyRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | não |
| apiKey | string | não |
| isEnabled | boolean | não |
| isDefault | boolean | não |
| models | array of string | não |

### UpdateChatSettingsRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| provider | string | não |
| model | string | não |

### UpdateDefaultLlmSelectionRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| providerName | string | não |
| model | string | não |

### UpdateFidesPolicyRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| enabledDetectors | object | não |

### UpdateGoldenSetDto

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | sim |
| description | string | não |
| agentName | string | sim |
| cases | array of GoldenSetCaseDto | sim |

### UpdateOnnxModelRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | não |
| description | string | não |
| inputNodeName | string | não |
| outputNodeName | string | não |
| inputWidth | integer | não |
| inputHeight | integer | não |
| channels | integer | não |
| scaleFactor | number | não |
| meanRed | number | não |
| meanGreen | number | não |
| meanBlue | number | não |
| outputFormat | string | não |
| isActive | boolean | não |

### UpdateProviderRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| apiKey | string | não |
| defaultModel | string | não |
| enabled | boolean | não |
| priority | integer | não |
| discoveredModels | array of string | não |

### UpdateReRankingSettingsRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| enabled | boolean | não |
| useDedicatedProvider | boolean | não |
| dedicatedProvider | string | não |
| dedicatedProviderBaseUrl | string | não |
| dedicatedProviderModel | string | não |
| hasDedicatedProviderApiKey | boolean | não |
| dedicatedProviderTimeoutSeconds | integer | não |
| localOnnxModelPath | string | não |
| localOnnxVocabularyPath | string | não |
| localOnnxMaxSequenceLength | integer | não |
| localOnnxMaxQueryTokens | integer | não |
| localOnnxLowerCase | boolean | não |
| localOnnxInputIdsName | string | não |
| localOnnxAttentionMaskName | string | não |
| localOnnxTokenTypeIdsName | string | não |
| localOnnxOutputName | string | não |
| localOnnxPositiveLabelIndex | integer | não |
| useEmbeddingReRanking | boolean | não |
| useLlmReRanking | boolean | não |
| candidatePoolSize | integer | não |
| minCandidateCountForLlm | integer | não |
| maxSnippetCharacters | integer | não |
| maxOutputTokens | integer | não |
| temperature | number | não |
| heuristicConfidenceThreshold | number | não |
| heuristicConfidenceGap | number | não |
| neuralScoreWeight | number | não |
| llmScoreWeight | number | não |
| hasUploadedLocalOnnxModel | boolean | não |
| uploadedLocalOnnxModelFileName | string | não |
| hasUploadedLocalOnnxVocabulary | boolean | não |
| uploadedLocalOnnxVocabularyFileName | string | não |
| dedicatedProviderApiKey | string | não |

### UpdateSessionTitleRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| title | string | não |

### UpdateSkillRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| name | string | não |
| domain | string | não |
| systemPromptFragment | string | não |
| fewShotExamples | string | não |
| metadata | object | não |

### UpdateTenantPlanRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| plan | string | não |

### VoiceRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| text | string | não |
| userId | string | não |
| userName | string | não |
| locale | string | não |

### VoiceResponse

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| text | string | não |
| success | boolean | não |

### WorkflowDefinition

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| name | string | não |
| description | string | não |
| version | integer | não |
| steps | array of WorkflowStep | não |
| edges | array of WorkflowEdge | não |
| promptTemplate | string | não |
| variables | object | não |
| triggerType | WorkflowTriggerType | não |
| cronExpression | string | não |
| createdAt | string | não |

### WorkflowEdge

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| fromStepId | string | não |
| toStepId | string | não |
| conditionExpression | string | não |

### WorkflowErrorStrategy

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### WorkflowExecutionStatus

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### WorkflowStep

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| id | string | não |
| name | string | não |
| stepType | WorkflowStepType | não |
| agentName | string | não |
| toolName | string | não |
| actionDescription | string | não |
| input | object | não |
| output | object | não |
| modelOverride | string | não |
| allowedToolsOverride | array of string | não |
| dependsOn | array of string | não |
| conditionExpression | string | não |
| parallelSteps | array of WorkflowStep | não |
| compensationStep | WorkflowStep | não |
| maxRetries | integer | não |
| timeout | string | não |
| errorStrategy | WorkflowErrorStrategy | não |

### WorkflowStepType

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### WorkflowTriggerType

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|

### YamlRequest

| Campo | Tipo/schema | Obrigatório no OpenAPI |
|---|---|---|
| yaml | string | não |
