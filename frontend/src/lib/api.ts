import { getAuthHeaders } from '@/lib/auth'

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

  if (!(options?.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers,
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

// ══════════════════════════════════════
// LLM API
// ══════════════════════════════════════

export const llmApi = {
  configuration: () => get<LLMConfigurationInfo>('/api/admin/llm/configuration'),
  providers: () => get<LLMProviderInfo[]>('/api/admin/llm/providers'),
  provider: (name: string) => get<LLMProviderInfo>(`/api/admin/llm/providers/${encodeURIComponent(name)}`),
  enabled: () => get<LLMProviderSummary[]>('/api/admin/llm/providers/enabled'),
  default: () => get<LLMProviderSummary>('/api/admin/llm/providers/default'),
  test: (name: string) => post<{ provider: string; available: boolean }>(`/api/admin/llm/providers/${encodeURIComponent(name)}/test`),
  update: (name: string, req: UpdateProviderRequest) =>
    put<LLMProviderInfo>(`/api/admin/llm/providers/${encodeURIComponent(name)}`, req),
  updateDefaultSelection: (req: UpdateDefaultLlmSelectionRequest) =>
    put<LLMConfigurationInfo>('/api/admin/llm/default-selection', req),
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

export { ApiError }
