import { get, post } from './client'
import type {
  GatewayDashboard,
  ServiceStatus,
  CostReport,
  HealthReport,
  SystemAlert
} from '@/types/api'

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
}
