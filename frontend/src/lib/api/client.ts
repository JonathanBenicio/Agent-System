import { useAuthStore } from '@/store/authStore'

const BASE_URL = import.meta.env.VITE_API_BASE_URL || ''

export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
    this.name = 'ApiError'
  }
}

export async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const headers = new Headers(options?.headers)

  // Use token from AuthStore (Supabase JWT)
  const token = useAuthStore.getState().token
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  // Enforce Tenant ID context from the Knowledge Store for Multi-tenancy isolation
  try {
    const store = await import('@/store/useKnowledgeStore');
    const tenantId = store.useKnowledgeStore.getState().activeWorkspaceId;
    if (tenantId) {
      headers.set('X-Tenant-Id', tenantId);
    }
  } catch {
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
      void useAuthStore.getState().logout().catch(() => {
        console.warn('Não foi possível encerrar a sessão após erro de autenticação.')
      })
    }
    const body = await res.text()
    let message = body || res.statusText
    try {
      const parsed = JSON.parse(body) as { error?: string; detail?: string; title?: string }
      message = parsed.error ?? parsed.detail ?? parsed.title ?? message
    } catch {
      // Keep the provider's plain-text error when the response is not JSON.
    }
    throw new ApiError(res.status, message)
  }

  if (res.status === 204) return undefined as T
  return res.json()
}

export function get<T>(path: string) {
  return request<T>(path)
}

export function post<T>(path: string, body?: unknown) {
  return request<T>(path, {
    method: 'POST',
    body: body ? JSON.stringify(body) : undefined,
  })
}

export function postForm<T>(path: string, body: FormData) {
  return request<T>(path, {
    method: 'POST',
    body,
  })
}

export function put<T>(path: string, body: unknown) {
  return request<T>(path, {
    method: 'PUT',
    body: JSON.stringify(body),
  })
}

export function del<T = void>(path: string) {
  return request<T>(path, { method: 'DELETE' })
}
