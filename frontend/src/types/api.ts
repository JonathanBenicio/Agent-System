// ══════════════════════════════════════
// Enums
// ══════════════════════════════════════

export const AgentTier = {
  Chief: 0,
  Master: 1,
  Specialist: 2,
  Support: 3,
} as const

export type AgentTier = (typeof AgentTier)[keyof typeof AgentTier]

export const TierLabels: Record<number, string> = {
  0: 'Chief',
  1: 'Master',
  2: 'Specialist',
  3: 'Support',
}

export const TierColors: Record<number, string> = {
  0: 'bg-teal-600',
  1: 'bg-blue-600',
  2: 'bg-emerald-600',
  3: 'bg-amber-600',
}

export const CircuitState = {
  Closed: 'Closed',
  Open: 'Open',
  HalfOpen: 'HalfOpen',
} as const

export type CircuitState = (typeof CircuitState)[keyof typeof CircuitState]

export const AutonomyLevel = {
  Manual: 0,
  Assisted: 1,
  Supervised: 2,
  SemiAutonomous: 3,
  Autonomous: 4,
  FullAutonomy: 5,
} as const

export type AutonomyLevel = (typeof AutonomyLevel)[keyof typeof AutonomyLevel]

export const AutonomyLabels: Record<number, string> = {
  0: 'L0 - Manual',
  1: 'L1 - Assistido',
  2: 'L2 - Supervisionado',
  3: 'L3 - Semi-Autônomo',
  4: 'L4 - Autônomo',
  5: 'L5 - Autonomia Total',
}

export const AutonomyColors: Record<number, string> = {
  0: 'bg-zinc-600 text-zinc-100',
  1: 'bg-sky-600 text-sky-100',
  2: 'bg-emerald-600 text-emerald-100',
  3: 'bg-indigo-600 text-indigo-100',
  4: 'bg-orange-600 text-orange-100',
  5: 'bg-red-600 text-red-100',
}

// ══════════════════════════════════════
// Agent Models
// ══════════════════════════════════════

export interface AgentInfo {
  name: string
  description: string
  tier: AgentTier
  domain: string
  createdAt: string
  lastUsedAt: string
  isActive: boolean
  availableTools: string[]
  configuration: Record<string, unknown>
  capabilities: string[]
  systemPrompt: string
  maxConcurrency: number
  timeoutSeconds: number
  toolNames?: string[]
  skillNames?: string[]
  autonomyLevel?: AutonomyLevel
}

export interface AgentSpecification {
  name: string
  description: string
  tier: AgentTier
  domain: string
  allowedTools?: string[]
  instructions?: string
  configuration?: Record<string, unknown>
  autoCleanupAfter?: string
  capabilities: string[]
  systemPrompt: string
  maxConcurrency: number
  timeoutSeconds: number
  autonomyLevel?: AutonomyLevel
}

export interface AgentEvent {
  id: string
  sessionId: string
  agentName: string
  agentTier: AgentTier
  userInput: string
  agentResponse: string
  actionsPerformed: string[]
  toolsUsed: string[]
  timestamp: string
  context: Record<string, unknown>
  tags: string[]
  relatedEvents: string[]
}

// ══════════════════════════════════════
// Tool Models
// ══════════════════════════════════════

export interface ToolInput {
  action: string
  parameters: Record<string, unknown>
  userId?: string
}

export interface ToolResult {
  success: boolean
  data?: unknown
  output?: unknown
  errorMessage?: string
  metadata?: Record<string, unknown>
}

export interface ToolSummary {
  id: string
  name: string
  description: string
  category: string
  requiresAuth: boolean
  isAsync?: boolean
}

// ══════════════════════════════════════
// Skill Models
// ══════════════════════════════════════

export interface SkillSummary {
  id: string
  name: string
  description?: string
  domain?: string
  type: string
  agentName?: string
}

export interface SkillContent {
  systemPrompt: string
  examples?: string
  metadata?: Record<string, string>
}

// ══════════════════════════════════════
// Gateway Models
// ══════════════════════════════════════

export interface ServiceStatus {
  name: string
  category: string
  isEnabled: boolean
  isHealthy: boolean
  circuitState: CircuitState
  requestCount: number
  failureCount: number
  successRate: number
  averageLatency: string
  totalCost: number
  lastChecked: string
}

export interface CostReport {
  totalCost: number
  dailyBudget: number
  usagePercent: number
  costByService: Record<string, number>
  costByCategory: Record<string, number>
  costByModel: Record<string, number>
  periodStart: string
  periodEnd: string
  budgetAlert: boolean
}

export interface HealthReport {
  overallHealthy: boolean
  totalServices: number
  healthyServices: number
  unhealthyServices: number
  services: ServiceHealthEntry[]
  timestamp: string
}

export interface ServiceHealthEntry {
  name: string
  category?: string
  isHealthy: boolean
  lastError?: string
  lastCheck: string
  latency?: string
  consecutiveFailures: number
}

export interface GatewayMetrics {
  totalRequests: number
  totalFailures: number
  overallSuccessRate: number
  averageLatency: string
  uptime: string
}

export interface GatewayDashboard {
  health: HealthReport
  costs: CostReport
  services: ServiceStatus[]
  metrics: GatewayMetrics
  timestamp: string
}

// ══════════════════════════════════════
// LLM Models
// ══════════════════════════════════════

export interface LLMProviderInfo {
  name: string
  defaultModel: string
  isEnabled: boolean
  priority: number
  hasApiKey: boolean
  isDefault: boolean
  isAvailable: boolean
  models: string[]
  currentBalance?: number
  requestsRemaining?: number
  tokensRemaining?: number
  quotaExceeded?: boolean
  lastQuotaUpdate?: string
}

export interface LLMConfigurationInfo {
  defaultProvider: string
  defaultModel: string
  providers: LLMProviderInfo[]
}

export interface LLMProviderSummary {
  name: string
  defaultModel: string
  isEnabled: boolean
  priority: number
}

export interface UpdateProviderRequest {
  apiKey?: string
  defaultModel?: string
  enabled?: boolean
  priority?: number
  discoveredModels?: string[]
}

export interface UpdateDefaultLlmSelectionRequest {
  providerName: string
  model?: string
}

// ══════════════════════════════════════
// MCP Plugin Models
// ══════════════════════════════════════

export interface PluginSummary {
  id: string
  name: string
  description: string
  version: string
  isEnabled: boolean
  isConnected?: boolean
  transport?: string
  toolCount?: number
  providedTools: string[]
  providedResources: string[]
  tools?: { name: string; description?: string }[]
}

export interface LoadPluginRequest {
  pluginPath?: string
  name?: string
  command?: string
  args?: string[]
  transport?: 'stdio' | 'sse'
  url?: string
}

export interface MCPToolInfo {
  pluginId: string
  pluginName: string
  name: string
  toolName?: string
  description: string
}

export interface MCPResponse {
  success: boolean
  data?: unknown
  errorMessage?: string
  metadata: Record<string, unknown>
}

// ══════════════════════════════════════
// Settings Models
// ══════════════════════════════════════

export interface GatewaySettings {
  defaultDailyBudget: number
  defaultFailureThreshold: number
  defaultBreakDurationSeconds: number
  defaultRequestsPerMinute: number
}

export interface MemorySettings {
  obsidianVaultPath: string
  vectorStoreType: string
  connectionString?: string
}

export interface RerankingSettings {
  enabled: boolean
  useDedicatedProvider: boolean
  dedicatedProvider: string
  dedicatedProviderBaseUrl: string
  dedicatedProviderModel: string
  hasDedicatedProviderApiKey: boolean
  dedicatedProviderApiKey?: string
  dedicatedProviderTimeoutSeconds: number
  localOnnxModelPath?: string
  localOnnxVocabularyPath?: string
  localOnnxMaxSequenceLength: number
  localOnnxMaxQueryTokens: number
  localOnnxLowerCase: boolean
  localOnnxInputIdsName: string
  localOnnxAttentionMaskName: string
  localOnnxTokenTypeIdsName: string
  localOnnxOutputName: string
  localOnnxPositiveLabelIndex: number
  useEmbeddingReRanking: boolean
  useLlmReRanking: boolean
  candidatePoolSize: number
  minCandidateCountForLlm: number
  maxSnippetCharacters: number
  maxOutputTokens: number
  temperature: number
  heuristicConfidenceThreshold: number
  heuristicConfidenceGap: number
  neuralScoreWeight: number
  llmScoreWeight: number
  hasUploadedLocalOnnxModel: boolean
  uploadedLocalOnnxModelFileName?: string
  hasUploadedLocalOnnxVocabulary: boolean
  uploadedLocalOnnxVocabularyFileName?: string
}

export interface ProviderSettingsView {
  baseUrl: string
  defaultModel: string
  enabled: boolean
  priority: number
  hasApiKey?: boolean
}

export interface SystemSettings {
  openAI: ProviderSettingsView
  ollama: ProviderSettingsView
  gemini: ProviderSettingsView
  claude: ProviderSettingsView
  gateway: GatewaySettings
  memory: MemorySettings
  reranking: RerankingSettings
}

// ══════════════════════════════════════
// Error
// ══════════════════════════════════════

export interface ErrorResponse {
  error: string
}

// ══════════════════════════════════════
// Scheduled Tasks & Triggers (ML21)
// ══════════════════════════════════════

export const ScheduledTaskStatus = {
  Active: 'Active',
  Paused: 'Paused',
  Completed: 'Completed',
  Failed: 'Failed',
  Disabled: 'Disabled',
} as const

export type ScheduledTaskStatus = (typeof ScheduledTaskStatus)[keyof typeof ScheduledTaskStatus]

export const DeliveryStatus = {
  Success: 'Success',
  Failed: 'Failed',
  Retrying: 'Retrying',
  Skipped: 'Skipped',
} as const

export type DeliveryStatus = (typeof DeliveryStatus)[keyof typeof DeliveryStatus]

export const TriggerSourceType = {
  Http: 'Http',
  Queue: 'Queue',
  FileSystem: 'FileSystem',
  Database: 'Database',
} as const

export type TriggerSourceType = (typeof TriggerSourceType)[keyof typeof TriggerSourceType]

export const ConditionType = {
  JsonPath: 'JsonPath',
  Threshold: 'Threshold',
  Regex: 'Regex',
  Contains: 'Contains',
} as const

export type ConditionType = (typeof ConditionType)[keyof typeof ConditionType]

export interface TriggerSource {
  type: TriggerSourceType
  endpoint: string
  method?: string
  headers?: Record<string, string>
}

export interface TriggerCondition {
  type: ConditionType
  expression: string
  expectedValue?: string
  operator?: string
}

export interface TriggerAction {
  type: string
  configuration: Record<string, string>
}

export interface TriggerRule {
  id: string
  name: string
  description?: string
  schedule?: string
  source: TriggerSource
  condition: TriggerCondition
  action?: TriggerAction
  deliveryChannels?: Record<string, Record<string, string>>
  enabled: boolean
  createdAt: string
  lastTriggeredAt?: string
  executionCount: number
  timeZoneId?: string
}

export interface ScheduledTask {
  id: string
  name: string
  schedule: string
  interval?: string
  status: ScheduledTaskStatus
  createdAt: string
  nextRunAt?: string
  lastRunAt?: string
  totalExecutions: number
  failedExecutions: number
  associatedRule?: TriggerRule
  timeZoneId?: string
}

export interface TaskExecution {
  id: string
  taskId: string
  startedAt: string
  completedAt?: string
  success: boolean
  errorMessage?: string
  evaluationResult?: TriggerEvaluationResult
}

export interface TriggerEvaluationResult {
  ruleId: string
  ruleName: string
  triggered: boolean
  actualValue?: string
  expectedValue?: string
  evaluatedAt: string
  errorMessage?: string
}

export interface DeliveryResult {
  channelName: string
  status: DeliveryStatus
  deliveredAt?: string
  attempts: number
  httpStatusCode?: number
  errorMessage?: string
}

export interface ChannelInfo {
  name: string
  healthy: boolean
}

export interface ScheduledTasksHealthReport {
  totalTasks: number
  activeTasks: number
  pausedTasks: number
  failedTasks: number
  totalRules: number
  enabledRules: number
  channels: ChannelInfo[]
  overallHealthy: boolean
}

export interface CreateTaskRequest {
  name: string
  cronExpression?: string
  intervalSeconds?: number
  associatedRule?: TriggerRule
}

// ══════════════════════════════════════
// Agent Versioning & YAML validation
// ══════════════════════════════════════

export const AgentVersionStatus = {
  Draft: 0,
  Active: 1,
  Promoted: 2,
  Deprecated: 3,
  RolledBack: 4,
} as const

export type AgentVersionStatus = (typeof AgentVersionStatus)[keyof typeof AgentVersionStatus]

export const AgentVersionStatusLabels: Record<number, string> = {
  0: 'Rascunho',
  1: 'Ativo',
  2: 'Promovido',
  3: 'Depreciado',
  4: 'Restaurado (Rollback)',
}

export const AgentVersionEnvironment = {
  Staging: 0,
  Production: 1,
  Canary: 2,
} as const

export type AgentVersionEnvironment = (typeof AgentVersionEnvironment)[keyof typeof AgentVersionEnvironment]

export interface AgentVersion {
  id: string
  agentName: string
  versionNumber: number
  label: string
  status: AgentVersionStatus
  environment: AgentVersionEnvironment
  systemPrompt: string
  modelProvider?: string
  modelId?: string
  tools: string[]
  parameters: Record<string, unknown>
  description?: string
  changeLog?: string
  createdBy?: string
  createdAt: string
  parentVersionId?: string
}

export interface YamlValidationError {
  line: number
  column: number
  errorCode: string
  message: string
  severity: string
}

export interface YamlValidationResult {
  isValid: boolean
  errors: YamlValidationError[]
  specification?: AgentSpecification
}

// ══════════════════════════════════════
// Workflow Engine Models
// ══════════════════════════════════════

export interface WorkflowDefinitionSummary {
  id: string;
  name: string;
  version: number;
  createdAt: string;
}

export interface WorkflowDefinition {
  id: string;
  name: string;
  description?: string;
  version: number;
  steps: WorkflowStep[];
  variables: Record<string, unknown>;
  triggerType: number;
  cronExpression?: string;
  createdAt: string;
}

export interface WorkflowStep {
  id: string;
  name: string;
  stepType: number;
  agentName?: string;
  toolName?: string;
  actionDescription?: string;
  input: Record<string, unknown>;
  output: Record<string, unknown>;
  dependsOn: string[];
  conditionExpression?: string;
  parallelSteps: WorkflowStep[];
  compensationStep?: WorkflowStep;
  maxRetries: number;
  timeout?: string;
  errorStrategy: number;
}

export interface WorkflowStepExecution {
  stepId: string;
  stepName: string;
  status: number;
  output: Record<string, unknown>;
  errorMessage?: string;
  retryCount: number;
  compensationExecuted: boolean;
  startedAt?: string;
  completedAt?: string;
}

export interface WorkflowExecution {
  id: string;
  workflowId: string;
  workflowName: string;
  status: number;
  stepExecutions: WorkflowStepExecution[];
  variables: Record<string, unknown>;
  initiatedBy?: string;
  errorMessage?: string;
  startedAt: string;
  completedAt?: string;
  duration?: string;
}

// ══════════════════════════════════════
// RAG & Embedding Migration Models
// ══════════════════════════════════════

export interface KnowledgeRoom {
  id: string;
  name: string;
  description: string;
  color: string;
  icon: string;
  documentCount: number;
  tags: string[];
  createdAt: string;
  updatedAt: string;
}

export interface RagStats {
  totalChunks: number;
  searchCount24h: number;
  onnxStatus: {
    loaded: boolean;
    modelName: string;
    hardware: string;
    avgLatencyMs: number;
  };
}

export interface IngestDocumentResponse {
  documentId: string
  fileName: string
  chunksCreated: number
  tokensProcessed: number
  contentHash: string
  durationMs: number
}

export interface EmbeddingModelConfig {
  id: string
  name: string
  provider: string
  modelName: string
  dimensions: number
  baseUrl?: string
  apiKey?: string
  isActive: boolean
  createdAt: string
}

export interface StartMigrationRequest {
  sourceModelId: string
  targetModelId: string
  batchSize?: number
  maxConcurrency?: number
  targetCollectionName?: string
}

export interface MigrationJob {
  id: string
  sourceModelId: string
  targetModelId: string
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Cancelled' | 'Switching'
  totalDocuments: number
  processedDocuments: number
  failedDocuments: number
  estimatedRemainingSeconds?: number
  errorMessage?: string
  startedAt: string
  completedAt?: string
}

export interface SystemAlert {
  id: string
  type: string
  severity: string
  message: string
  providerName: string
  percentage: number
  createdAt: string
  isRead: boolean
}

// ══════════════════════════════════════
// Session Models
// ══════════════════════════════════════

export interface SessionListItem {
  id: string
  title: string
  lastActivity: string
  messageCount: number
  summary?: string
}

export interface SessionDetail {
  id: string
  title: string
  startedAt: string
  endedAt?: string
  messages: ChatMessageDto[]
  summary?: SessionSummaryDto
  insights?: SessionInsightsDto
}

export interface ChatMessageDto {
  id: string
  role: string
  content: string
  agentName?: string
  agentTier?: number
  actions?: string[]
  tools?: string[]
  success?: boolean
  timestamp: string
}

export interface SessionSummaryDto {
  summary: string
  topicsDiscussed: string[]
  agentsUsed: string[]
  eventCount: number
}

export interface SessionInsightsDto {
  facts: string[]
  decisions: string[]
  preferences: string[]
  actionItems: string[]
}
