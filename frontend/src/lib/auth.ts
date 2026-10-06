const TOKEN_KEY = 'agentic_auth_token'
const BASE_URL = import.meta.env.VITE_API_BASE_URL || ''

export interface CookieSession {
  userId: string
  tenantId: string
  roles: string[]
}

export function getAuthToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setAuthToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearAuthToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

/** Removes credentials stored by older clients without reading or retransmitting them. */
export function clearLegacyApiKey(): void {
  localStorage.removeItem('agentic_api_key')
  localStorage.removeItem('agentic_api_key_subject')
}

/** Authenticates the HttpOnly cookie and returns identity, never the credential. */
export async function getCookieSession(): Promise<CookieSession | null> {
  const res = await fetch(`${BASE_URL}/api/auth/session`, { credentials: 'include' })
  if (res.status === 401 || res.status === 403) return null
  if (!res.ok) throw new Error('Não foi possível verificar a sessão.')
  const session = await res.json() as CookieSession
  return session.userId && session.tenantId ? session : null
}

export async function loginWithApiKeyApi(apiKey: string): Promise<CookieSession | null> {
  clearLegacyApiKey()
  const res = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ apiKey }),
    credentials: 'include',
  })
  if (!res.ok) return null
  return getCookieSession()
}

export async function logoutApi(): Promise<void> {
  const res = await fetch(`${BASE_URL}/api/auth/logout`, {
    method: 'POST',
    credentials: 'include',
  })
  if (!res.ok) throw new Error('Não foi possível encerrar a sessão.')
}

/** API-key sessions use cookies; only explicit JWT mode needs an auth header. */
export function getAuthHeaders(): Record<string, string> {
  const token = getAuthToken()
  return token ? { Authorization: `Bearer ${token}` } : {}
}
