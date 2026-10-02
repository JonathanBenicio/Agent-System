import { get, post, put, del, postForm } from './client'
import type {
  WorkflowDefinitionSummary,
  WorkflowDefinition,
  WorkflowExecution,
  KnowledgeRoom,
  KnowledgeRoomPermission,
  KnowledgeRoomRole,
  RagStats,
  IngestDocumentResponse,
  EmbeddingModelConfig,
  MigrationJob,
  StartMigrationRequest,
  OnnxModelSummary,
  OnnxModelDetail,
  OnnxInspectResult,
  OnnxJobsPagedResponse,
  PluginSummary,
  LoadPluginRequest,
  MCPToolInfo,
  MCPResponse
} from '@/types/api'

export const workflowApi = {
  listDefinitions: (limit?: number) => get<WorkflowDefinitionSummary[]>(`/api/workflow/definitions${limit ? `?limit=${limit}` : ''}`),
  getDefinition: (id: string) => get<WorkflowDefinition>(`/api/workflow/definitions/${encodeURIComponent(id)}`),
  saveDefinition: (def: WorkflowDefinition) => post<WorkflowDefinition>('/api/workflow/definitions', def),
  deleteDefinition: (id: string) => del(`/api/workflow/definitions/${encodeURIComponent(id)}`),
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
  approveStep: (executionId: string) => post<void>(`/api/workflow/executions/${encodeURIComponent(executionId)}/approve`),
  rejectStep: (executionId: string) => post<void>(`/api/workflow/executions/${encodeURIComponent(executionId)}/reject`),
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
  ingest: (file: File, source?: string, roomId?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    const params = new URLSearchParams()
    if (source) params.set('source', source)
    if (roomId) params.set('roomId', roomId)
    const qs = params.size ? `?${params.toString()}` : ''
    return postForm<IngestDocumentResponse>(`/api/document/ingest${qs}`, formData)
  },
  ingestBatch: (files: FileList | File[], source?: string, roomId?: string) => {
    const formData = new FormData()
    for (let i = 0; i < files.length; i++) {
      formData.append('files', files[i])
    }
    const params = new URLSearchParams()
    if (source) params.set('source', source)
    if (roomId) params.set('roomId', roomId)
    const qs = params.size ? `?${params.toString()}` : ''
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

export const pluginApi = {
  list: () => get<PluginSummary[]>('/api/admin/plugins'),
  get: (id: string) => get<PluginSummary>(`/api/admin/plugins/${encodeURIComponent(id)}`),
  load: (req: LoadPluginRequest) => post<PluginSummary>('/api/admin/plugins/load', req),
  update: (id: string, req: LoadPluginRequest) => put<{ success: boolean; message: string }>(`/api/admin/plugins/${encodeURIComponent(id)}`, req),
  delete: (id: string) => del(`/api/admin/plugins/${encodeURIComponent(id)}`),
  tools: () => get<MCPToolInfo[]>('/api/admin/plugins/tools'),
  executeTool: (pluginId: string, toolName: string, params: Record<string, unknown>) =>
    post<MCPResponse>(`/api/admin/plugins/${encodeURIComponent(pluginId)}/tools/${encodeURIComponent(toolName)}/execute`, params),
  resources: (id: string) => get<string[]>(`/api/admin/plugins/${encodeURIComponent(id)}/resources`),
}

export const onnxModelApi = {
  list: () => get<OnnxModelSummary[]>('/api/onnx/models'),
  get: (id: string) => get<OnnxModelDetail>(`/api/onnx/models/${encodeURIComponent(id)}`),
  create: (formData: FormData) => postForm<OnnxModelSummary>('/api/onnx/models', formData),
  update: (id: string, data: Partial<OnnxModelDetail>) => put<OnnxModelDetail>(`/api/onnx/models/${encodeURIComponent(id)}`, data),
  delete: (id: string) => del(`/api/onnx/models/${encodeURIComponent(id)}`),
  inspect: (id: string) => post<OnnxInspectResult>(`/api/onnx/models/${encodeURIComponent(id)}/inspect`),
  test: (id: string, formData: FormData) => postForm<{ jobId: string; status: string }>(`/api/onnx/models/${encodeURIComponent(id)}/test`, formData),
  listJobs: (modelId?: string, page = 1, pageSize = 10) => {
    const params = new URLSearchParams()
    if (modelId) params.set('modelId', modelId)
    params.set('page', page.toString())
    params.set('pageSize', pageSize.toString())
    return get<OnnxJobsPagedResponse>(`/api/onnx/models/jobs?${params.toString()}`)
  },
  deleteJob: (jobId: string) => del<void>(`/api/onnx/models/jobs/${encodeURIComponent(jobId)}`),
}
