import { get, post, put, del } from './client'
import type {
  LLMConfigurationInfo,
  LLMProviderInfo,
  LLMProviderSummary,
  UpdateProviderRequest,
  UpdateDefaultLlmSelectionRequest,
  LLMProviderApiKey,
  RegisterApiKeyRequest,
  UpdateApiKeyRequest
} from '@/types/api'

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
  
  // API Keys
  getKeys: (providerName: string) => 
    get<LLMProviderApiKey[]>(`/api/admin/llm/providers/${encodeURIComponent(providerName)}/keys`),
  registerKey: (providerName: string, req: RegisterApiKeyRequest) => 
    post<LLMProviderApiKey>(`/api/admin/llm/providers/${encodeURIComponent(providerName)}/keys`, req),
  updateKey: (providerName: string, id: string, req: UpdateApiKeyRequest) => 
    put<LLMProviderApiKey>(`/api/admin/llm/providers/${encodeURIComponent(providerName)}/keys/${encodeURIComponent(id)}`, req),
  deleteKey: (providerName: string, id: string) => 
    del(`/api/admin/llm/providers/${encodeURIComponent(providerName)}/keys/${encodeURIComponent(id)}`),
  setDefaultKey: (providerName: string, id: string) => 
    post<{ message: string }>(`/api/admin/llm/providers/${encodeURIComponent(providerName)}/keys/${encodeURIComponent(id)}/default`),
  testKey: (providerName: string, id: string) => 
    post<{ success: boolean }>(`/api/admin/llm/providers/${encodeURIComponent(providerName)}/keys/${encodeURIComponent(id)}/test`),
  discoverModelsForKey: (providerName: string, id: string) => 
    post<{ success: boolean; discoveredModels: string[]; errorMessage?: string }>(`/api/admin/llm/providers/${encodeURIComponent(providerName)}/keys/${encodeURIComponent(id)}/discover-models`),
}
