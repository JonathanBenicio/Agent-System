import { get, post, put, del, postForm } from './client'
import type {
  SystemSettings,
  GatewaySettings,
  MemorySettings,
  RerankingSettings,
  ScheduledTask,
  CreateTaskRequest,
  TaskExecution,
  TriggerRule,
  ChannelInfo,
  DeliveryResult,
  ScheduledTasksHealthReport,
  InboundWebhook,
  HotSwapSubsystem,
  HotSwapResult
} from '@/types/api'

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
    if (modelFile) formData.append('modelFile', modelFile)
    if (vocabularyFile) formData.append('vocabularyFile', vocabularyFile)
    if (packageFile) formData.append('packageFile', packageFile)
    return postForm<RerankingSettings>('/api/admin/settings/reranking/assets', formData)
  },
}

export const scheduledTasksApi = {
  listTasks: () => get<ScheduledTask[]>('/api/admin/scheduled-tasks/tasks'),
  getTask: (id: string) => get<ScheduledTask>(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}`),
  createTask: (req: CreateTaskRequest) => post<ScheduledTask>('/api/admin/scheduled-tasks/tasks', req),
  pauseTask: (id: string) => post<void>(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}/pause`),
  resumeTask: (id: string) => post<void>(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}/resume`),
  executeTask: (id: string) => post<TaskExecution>(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}/execute`),
  deleteTask: (id: string) => del(`/api/admin/scheduled-tasks/tasks/${encodeURIComponent(id)}`),
  listRules: () => get<TriggerRule[]>('/api/admin/scheduled-tasks/rules'),
  getRule: (id: string) => get<TriggerRule>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}`),
  createRule: (rule: TriggerRule) => post<TriggerRule>('/api/admin/scheduled-tasks/rules', rule),
  updateRule: (id: string, rule: TriggerRule) => put<TriggerRule>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}`, rule),
  enableRule: (id: string) => post<void>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}/enable`),
  disableRule: (id: string) => post<void>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}/disable`),
  evaluateRule: (id: string) => post<unknown>(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}/evaluate`),
  deleteRule: (id: string) => del(`/api/admin/scheduled-tasks/rules/${encodeURIComponent(id)}`),
  listChannels: () => get<ChannelInfo[]>('/api/admin/scheduled-tasks/channels'),
  testChannel: (name: string, config: Record<string, string>) =>
    post<DeliveryResult>(`/api/admin/scheduled-tasks/channels/${encodeURIComponent(name)}/test`, config),
  health: () => get<ScheduledTasksHealthReport>('/api/admin/scheduled-tasks/health'),
}

export const webhookApi = {
  list: () => get<InboundWebhook[]>('/api/webhooks/admin'),
  create: (data: Partial<InboundWebhook>) => post<InboundWebhook>('/api/webhooks/admin', data),
  delete: (id: string) => del(`/api/webhooks/admin/${encodeURIComponent(id)}`),
}

export const configApi = {
  hotSwap: (subsystem: HotSwapSubsystem) =>
    post<HotSwapResult>('/api/admin/config/hot-swap', { subsystem }),
}
