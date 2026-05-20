import { getAuthHeaders } from '@/lib/auth'
import type { SystemAlert } from '@/types/api'

const BASE_URL = import.meta.env.VITE_API_BASE_URL || ''

class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
    this.name = 'ApiError'
  }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const headers = new Headers(options?.headers)

  for (const [key, value] of Object.entries(getAuthHeaders())) {
    headers.set(key, value)
  }

  // Enforce Tenant ID context from the Knowledge Store for Multi-tenancy isolation
  try {
    const store = await import('@/store/useKnowledgeStore');
    const tenantId = store.useKnowledgeStore.getState().activeWorkspaceId;
    if (tenantId) {
      headers.set('X-Tenant-Id', tenantId);
    }
  } catch (e) {
    // Ignore if store is not initialized
  }

  if (!(options?.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers,
    credentials: 'include',
  })

  if (!res.ok) {
    if (res.status === 401) {
      import('@/store/authStore').then(({ useAuthStore }) => {
        useAuthStore.getState().logout()
      })
    }
    const body = await res.text()
    throw new ApiError(res.status, body || res.statusText)
  }

  if (res.status === 204) return undefined as T
  return res.json()
}

function get<T>(path: string) {
  return request<T>(path)
}

function post<T>(path: string, body?: unknown) {
  return request<T>(path, {
    method: 'POST',
    body: body ? JSON.stringify(body) : undefined,
  })
}

function postForm<T>(path: string, body: FormData) {
  return request<T>(path, {
    method: 'POST',
    body,
  })
}

function put<T>(path: string, body: unknown) {
  return request<T>(path, {
    method: 'PUT',
    body: JSON.stringify(body),
  })
}

function del<T = void>(path: string) {
  return request<T>(path, { method: 'DELETE' })
}

// ══════════════════════════════════════
// Agent API
// ══════════════════════════════════════

import type {
  AgentInfo,
  AgentSpecification,
  AgentEvent,
  ToolSummary,
  ToolInput,
  ToolResult,
  SkillSummary,
  SkillContent,
  GatewayDashboard,
  ServiceStatus,
  CostReport,
  HealthReport,
  LLMConfigurationInfo,
  LLMProviderInfo,
  LLMProviderSummary,
  UpdateDefaultLlmSelectionRequest,
  UpdateProviderRequest,
  PluginSummary,
  LoadPluginRequest,
  MCPToolInfo,
  MCPResponse,
  SystemSettings,
  GatewaySettings,
  MemorySettings,
  RerankingSettings,
  ScheduledTask,
  TriggerRule,
  TaskExecution,
  ChannelInfo,
  ScheduledTasksHealthReport,
  CreateTaskRequest,
  DeliveryResult,
  AgentVersion,
  YamlValidationResult,
  WorkflowDefinitionSummary,
  WorkflowDefinition,
  WorkflowExecution,
  SessionListItem,
  SessionDetail,
  ChatMessageDto,
} from '@/types/api'

export const agentApi = {
  list: () => get<AgentInfo[]>('/api/agent/agents'),
  listAll: () => get<AgentInfo[]>('/api/agent/agents/all'),
  listByTier: (tier: number) => get<AgentInfo[]>(`/api/agent/agents/tier/${tier}`),
  get: (name: string) => get<AgentInfo>(`/api/agent/agents/${encodeURIComponent(name)}`),
  create: (spec: AgentSpecification) => post<AgentInfo>('/api/agent/agents', spec),
  update: (name: string, spec: AgentSpecification) =>
    put<AgentInfo>(`/api/agent/agents/${encodeURIComponent(name)}`, spec),
  delete: (name: string) => del(`/api/agent/agents/${encodeURIComponent(name)}`),
  validateYaml: (yaml: string) => post<YamlValidationResult>('/api/agent/agents/validate-yaml', { yaml }),
  saveYaml: (yaml: string) => post<{ agent: AgentInfo; version?: AgentVersion }>('/api/agent/agents/save-yaml', { yaml }),
  getHistory: (name: string, limit?: number) => get<AgentVersion[]>(`/api/agent/agents/${encodeURIComponent(name)}/history${limit ? `?limit=${limit}` : ''}`),
  rollback: (name: string, versionId: string) => post<{ message: string; agent: AgentInfo; version: AgentVersion }>(`/api/agent/agents/${encodeURIComponent(name)}/rollback/${encodeURIComponent(versionId)}`),
}

export const toolApi = {
  list: (category?: string) =>
    get<ToolSummary[]>(`/api/agent/tools${category ? `?category=${encodeURIComponent(category)}` : ''}`),
  get: (id: string) => get<ToolSummary>(`/api/agent/tools/${encodeURIComponent(id)}`),
  execute: (id: string, input: ToolInput) =>
    post<ToolResult>(`/api/agent/tools/${encodeURIComponent(id)}/execute`, input),
  delete: (id: string) => del(`/api/agent/tools/${encodeURIComponent(id)}`),
}

export const skillApi = {
  listAll: () => get<SkillSummary[]>('/api/agent/skills/all'),
  list: (agent?: string, domain?: string) => {
    const params = new URLSearchParams()
    if (agent) params.set('agent', agent)
    if (domain) params.set('domain', domain)
    const qs = params.toString()
    return get<SkillContent[]>(`/api/agent/skills${qs ? `?${qs}` : ''}`)
  },
  delete: (id: string) => del(`/api/agent/skills/${encodeURIComponent(id)}`),
}

export const sessionApi = {
  list: (limit?: number) =>
    get<SessionListItem[]>(`/api/session${limit ? `?limit=${limit}` : ''}`),
  get: (id: string) => get<SessionDetail>(`/api/session/${encodeURIComponent(id)}`),
  messages: (id: string) => get<ChatMessageDto[]>(`/api/session/${encodeURIComponent(id)}/messages`),
  delete: (id: string) => del(`/api/session/${encodeURIComponent(id)}`),
  updateTitle: (id: string, title: string) =>
    put<{ id: string; title: string }>(`/api/session/${encodeURIComponent(id)}/title`, { title }),
  getEvents: (sessionId: string, count?: number) =>
    get<AgentEvent[]>(`/api/agent/sessions/${encodeURIComponent(sessionId)}/events${count ? `?count=${count}` : ''}`),
  cleanup: () => post<{ message: string; timestamp: string }>('/api/agent/maintenance/cleanup'),
}

// ══════════════════════════════════════
// Gateway API
// ══════════════════════════════════════

export const gatewayApi = {
  dashboard: () => get<GatewayDashboard>('/api/admin/gateway/dashboard'),
  services: () => get<ServiceStatus[]>('/api/admin/gateway/services'),
  service: (name: string) => get<ServiceStatus>(`/api/admin/gateway/services/${encodeURIComponent(name)}`),
  servicesByCategory: (cat: string) =>
    get<ServiceStatus[]>(`/api/admin/gateway/services/category/${encodeURIComponent(cat)}`),
  costs: () => get<CostReport>('/api/admin/gateway/costs'),
  health: () => get<HealthReport>('/api/admin/gateway/health'),
  enable: (name: string) => post(`/api/admin/gateway/services/${encodeURIComponent(name)}/enable`),
  disable: (name: string) => post(`/api/admin/gateway/services/${encodeURIComponent(name)}/disable`),
}

export const alertsApi = {
  getAlerts: (limit?: number) => get<SystemAlert[]>(`/api/v1/alerts${limit ? `?limit=${limit}` : ''}`),
  markAsRead: (id: string) => post(`/api/v1/alerts/${encodeURIComponent(id)}/read`),
}// ══════════════════════════════════════
// LLM API
// ══════════════════════════════════════

export const llmApi = {
  configuration: () => get<LLMConfigurationInfo>('/api/admin/llm/configuration'),
  providers: () => get<LLMProviderInfo[]>('/api/admin/llm/providers'),
  provider: (name: string) => get<LLMProviderInfo>(`/api/admin/llm/providers/${encodeURIComponent(name)}`),
  enabled: () => get<LLMProviderSummary[]>('/api/admin/llm/providers/enabled'),
  default: () => get<LLMProviderSummary>('/api/admin/llm/providers/default'),
  test: (name: string) => post<{ provider: string; available: boolean }>(`/api/admin/llm/providers/${encodeURIComponent(name)}/test`),
  discoverModels: (name: string, apiKey: string) => post<{ success: boolean; discoveredModels: string[]; errorMessage?: string }>(`/api/admin/llm/providers/${encodeURIComponent(name)}/discover-models`, { apiKey }),
  update: (name: string, req: UpdateProviderRequest) =>
    put<LLMProviderInfo>(`/api/admin/llm/providers/${encodeURIComponent(name)}`, req),
  updateDefaultSelection: (req: UpdateDefaultLlmSelectionRequest) =>
    put<LLMConfigurationInfo>('/api/admin/llm/default-selection', req),
  syncQuotas: () => post<{ message: string }>('/api/admin/llm/providers/sync-quotas'),
}

// ══════════════════════════════════════
// Plugins API
// ══════════════════════════════════════

export const pluginApi = {
  list: () => get<PluginSummary[]>('/api/admin/plugins'),
  get: (id: string) => get<PluginSummary>(`/api/admin/plugins/${encodeURIComponent(id)}`),
  load: (req: LoadPluginRequest) => post<PluginSummary>('/api/admin/plugins/load', req),
  delete: (id: string) => del(`/api/admin/plugins/${encodeURIComponent(id)}`),
  tools: () => get<MCPToolInfo[]>('/api/admin/plugins/tools'),
  executeTool: (pluginId: string, toolName: string, params: Record<string, unknown>) =>
    post<MCPResponse>(`/api/admin/plugins/${encodeURIComponent(pluginId)}/tools/${encodeURIComponent(toolName)}/execute`, params),
  resources: (id: string) => get<string[]>(`/api/admin/plugins/${encodeURIComponent(id)}/resources`),
}

// ══════════════════════════════════════
// Settings API
// ══════════════════════════════════════

export const settingsApi = {
  getAll: () => get<SystemSettings>('/api/admin/settings'),
  getGateway: () => get<GatewaySettings>('/api/admin/settings/gateway'),
  getMemory: () => get<MemorySettings>('/api/admin/settings/memory'),
  getReranking: () => get<RerankingSettings>('/api/admin/settings/reranking'),
  getProviders: () => get('/api/admin/settings/providers'),
  updateGateway: (s: GatewaySettings) => put<GatewaySettings>('/api/admin/settings/gateway', s),
  updateMemory: (s: MemorySettings) => put<MemorySettings>('/api/admin/settings/memory', s),
  updateReranking: (s: RerankingSettings) => put<RerankingSettings>('/api/admin/settings/reranking', s),
  uploadRerankingAssets: (modelFile?: File, vocabularyFile?: File, packageFile?: File) => {
    const formData = new FormData()
    if (modelFile) {
      formData.append('modelFile', modelFile)
    }

    if (vocabularyFile) {
      formData.append('vocabularyFile', vocabularyFile)
    }

    if (packageFile) {
      formData.append('packageFile', packageFile)
    }

    return postForm<RerankingSettings>('/api/admin/settings/reranking/assets', formData)
  },
}

// ══════════════════════════════════════
// Scheduled Tasks API (ML21)
// ══════════════════════════════════════

export const scheduledTasksApi = {
  // Tasks
  listTasks: () => get<ScheduledTask[]>('/api/admin/scheduled-tasks/tasks'),
  getTask: (id: string) => get<ScheduledTask>(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}`),
  createTask: (req: CreateTaskRequest) => post<ScheduledTask>('/api/admin/scheduled-tasks/tasks', req),
  pauseTask: (id: string) => post<void>(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}/pause`),
  resumeTask: (id: string) => post<void>(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}/resume`),
  executeTask: (id: string) => post<TaskExecution>(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}/execute`),
  deleteTask: (id: string) => del(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}`),
  // Rules
  listRules: () => get<TriggerRule[]>('/api/admin/scheduled-tasks/rules'),
  getRule: (id: string) => get<TriggerRule>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}`),
  createRule: (rule: TriggerRule) => post<TriggerRule>('/api/admin/scheduled-tasks/rules', rule),
  updateRule: (id: string, rule: TriggerRule) => put<TriggerRule>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}`, rule),
  enableRule: (id: string) => post<void>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}/enable`),
  disableRule: (id: string) => post<void>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}/disable`),
  evaluateRule: (id: string) => post<unknown>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}/evaluate`),
  deleteRule: (id: string) => del(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}`),
  // Channels
  listChannels: () => get<ChannelInfo[]>('/api/admin/scheduled-tasks/channels'),
  testChannel: (name: string, config: Record<string, string>) =>
    post<DeliveryResult>(`/api/admin/scheduled-tasks/channels/${encodeURIComponent(name)}/test`, config),
  // Health
  health: () => get<ScheduledTasksHealthReport>('/api/admin/scheduled-tasks/health'),
}

// ══════════════════════════════════════
// RAG & Embedding Migration API
// ══════════════════════════════════════

import type {
  KnowledgeRoom,
  KnowledgeRoomRole,
  KnowledgeRoomPermission,
  RagStats,
  IngestDocumentResponse,
  EmbeddingModelConfig,
  StartMigrationRequest,
  MigrationJob,
} from '@/types/api'

export const workflowApi = {
  // Definitions
  listDefinitions: (limit?: number) => get<WorkflowDefinitionSummary[]>(`/api/workflow/definitions${limit ? `?limit=${limit}` : ''}`),
  getDefinition: (id: string) => get<WorkflowDefinition>(`/api/workflow/definitions/${encodeURIComponent(id)}`),
  saveDefinition: (def: WorkflowDefinition) => post<WorkflowDefinition>('/api/workflow/definitions', def),
  deleteDefinition: (id: string) => del(`/api/workflow/definitions/${encodeURIComponent(id)}`),

  // Executions
  startWorkflow: (definitionId: string, variables?: Record<string, unknown>) => 
    post<WorkflowExecution>(`/api/workflow/executions/start/${encodeURIComponent(definitionId)}`, variables),
  getExecution: (id: string) => get<WorkflowExecution>(`/api/workflow/executions/${encodeURIComponent(id)}`),
  listExecutions: (status?: number, limit?: number) => {
    const params = new URLSearchParams()
    if (status !== undefined) params.set('status', status.toString())
    if (limit) params.set('limit', limit.toString())
    const qs = params.toString()
    return get<WorkflowExecution[]>(`/api/workflow/executions${qs ? `?${qs}` : ''}`)
  },
  cancelExecution: (id: string, reason?: string) => 
    post<WorkflowExecution>(`/api/workflow/executions/${encodeURIComponent(id)}/cancel${reason ? `?reason=${encodeURIComponent(reason)}` : ''}`),
}

export const knowledgeRoomApi = {
  list: () => get<KnowledgeRoom[]>('/api/knowledge/rooms'),
  create: (data: Partial<KnowledgeRoom>) => post<KnowledgeRoom>('/api/knowledge/rooms', data),
  update: (id: string, data: Partial<KnowledgeRoom>) => put<KnowledgeRoom>(`/api/knowledge/rooms/${encodeURIComponent(id)}`, data),
  delete: (id: string) => del(`/api/knowledge/rooms/${encodeURIComponent(id)}`),
  getPermissions: (id: string) => get<KnowledgeRoomPermission[]>(`/api/knowledge/rooms/${encodeURIComponent(id)}/permissions`),
  updatePermission: (id: string, request: { userId: string; role: KnowledgeRoomRole }) => post<KnowledgeRoomPermission>(`/api/knowledge/rooms/${encodeURIComponent(id)}/permissions`, request),
  deletePermission: (id: string, targetUserId: string) => del(`/api/knowledge/rooms/${encodeURIComponent(id)}/permissions/${encodeURIComponent(targetUserId)}`),
}

export const ragApi = {
  stats: () => get<RagStats>('/api/document/stats'),
  ingest: (file: File, source?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    const qs = source ? `?source=${encodeURIComponent(source)}` : ''
    return postForm<IngestDocumentResponse>(`/api/document/ingest${qs}`, formData)
  },
  ingestBatch: (files: FileList | File[], source?: string) => {
    const formData = new FormData()
    for (let i = 0; i < files.length; i++) {
      formData.append('files', files[i])
    }
    const qs = source ? `?source=${encodeURIComponent(source)}` : ''
    return postForm<{ total: number; succeeded: number; failed: number; results: unknown[] }>(`/api/document/ingest/batch${qs}`, formData)
  },
}

export const embeddingMigrationApi = {
  models: () => get<EmbeddingModelConfig[]>('/api/admin/embedding-migration/models'),
  activeModel: () => get<EmbeddingModelConfig>('/api/admin/embedding-migration/models/active'),
  saveModel: (config: Partial<EmbeddingModelConfig>) => post<EmbeddingModelConfig>('/api/admin/embedding-migration/models', config),
  activateModel: (id: string) => post<{ message: string }>(`/api/admin/embedding-migration/models/${encodeURIComponent(id)}/activate`),
  deleteModel: (id: string) => del(`/api/admin/embedding-migration/models/${encodeURIComponent(id)}`),
  jobs: () => get<MigrationJob[]>('/api/admin/embedding-migration/jobs'),
  startJob: (req: StartMigrationRequest) => post<MigrationJob>('/api/admin/embedding-migration/jobs', req),
  cancelJob: (id: string) => post<{ message: string }>(`/api/admin/embedding-migration/jobs/${encodeURIComponent(id)}/cancel`),
  retryJob: (id: string) => post<{ message: string }>(`/api/admin/embedding-migration/jobs/${encodeURIComponent(id)}/retry`),
  switchCollection: (id: string) => post<{ message: string }>(`/api/admin/embedding-migration/jobs/${encodeURIComponent(id)}/switch`),
}

// ══════════════════════════════════════
// Webhook API
// ══════════════════════════════════════

export interface InboundWebhook {
  id: string;
  name: string;
  secret: string;
  targetWorkflowId?: string;
  targetAgentName?: string;
  isActive: boolean;
  createdAt: string;
  lastTriggeredAt?: string;
}

export const webhookApi = {
  list: () => get<InboundWebhook[]>('/api/webhooks/admin'),
  create: (data: Partial<InboundWebhook>) => post<InboundWebhook>('/api/webhooks/admin', data),
  delete: (id: string) => del(`/api/webhooks/admin/${encodeURIComponent(id)}`),
}

// ══════════════════════════════════════
// Config Hot-Swap API
// ══════════════════════════════════════

export type HotSwapSubsystem = 'vectorstore' | 'llm' | 'embedding' | 'all'

export interface HotSwapResult {
  subsystem: string
  status: string
  message: string
  timestamp: string
}

export const configApi = {
  hotSwap: (subsystem: HotSwapSubsystem) =>
    post<HotSwapResult>('/api/admin/config/hot-swap', { subsystem }),
}

export { ApiError }

