// ══════════════════════════════════════
// Enums
// ══════════════════════════════════════

export enum AgentTier {
  Chief = 0,
  Master = 1,
  Specialist = 2,
  Support = 3,
}

export const TierLabels: Record<number, string> = {
  0: 'Chief',
  1: 'Master',
  2: 'Specialist',
  3: 'Support',
}

export const TierColors: Record<number, string> = {
  0: 'bg-violet-600',
  1: 'bg-blue-600',
  2: 'bg-emerald-600',
  3: 'bg-amber-600',
}

export enum CircuitState {
  Closed = 'Closed',
  Open = 'Open',
  HalfOpen = 'HalfOpen',
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
  isDefault?: boolean
  isAvailable?: boolean
  models?: string[]
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
  maxRetryAttempts: number
  circuitBreakerThreshold: number
  circuitBreakerDuration: number
  dailyBudget: number
  defaultTimeout: number
  enableCostTracking: boolean
}

export interface MemorySettings {
  obsidianVaultPath: string
  vectorStoreType: string
  connectionString?: string
  maxSessions: number
  maxHistoryPerSession: number
  sessionTimeoutMinutes: number
  enablePersistence: boolean
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

export enum ScheduledTaskStatus {
  Active = 'Active',
  Paused = 'Paused',
  Completed = 'Completed',
  Failed = 'Failed',
  Disabled = 'Disabled',
}

export enum DeliveryStatus {
  Success = 'Success',
  Failed = 'Failed',
  Retrying = 'Retrying',
  Skipped = 'Skipped',
}

export enum TriggerSourceType {
  Http = 'Http',
  Queue = 'Queue',
  FileSystem = 'FileSystem',
  Database = 'Database',
}

export enum ConditionType {
  JsonPath = 'JsonPath',
  Threshold = 'Threshold',
  Regex = 'Regex',
  Contains = 'Contains',
}

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
