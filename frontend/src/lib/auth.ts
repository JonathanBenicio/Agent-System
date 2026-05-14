// ══════════════════════════════════════
// Auth token management
// ══════════════════════════════════════

const TOKEN_KEY = 'agentic_auth_token'
const API_KEY_KEY = 'agentic_api_key'
const BASE_URL = import.meta.env.VITE_API_BASE_URL || ''

export function getAuthToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setAuthToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearAuthToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

export function getApiKey(): string | null {
  return localStorage.getItem(API_KEY_KEY)
}

export function setApiKey(key: string): void {
  localStorage.setItem(API_KEY_KEY, key)
}

export function clearApiKey(): void {
  localStorage.removeItem(API_KEY_KEY)
}

/**
 * Realiza o login na API e emite o cookie httpOnly
 */
export async function loginWithApiKeyApi(apiKey: string): Promise<boolean> {
  try {
    const res = await fetch(`${BASE_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ apiKey }),
      credentials: 'include',
    })

    if (res.ok) {
      setApiKey('admin') // Salva apenas o indicador de sessão, não a chave real
      return true
    }
    return false
  } catch (err) {
    console.error('Falha ao autenticar via API', err)
    return false
  }
}

/**
 * Realiza o logout na API limpando o cookie httpOnly
 */
export async function logoutApi(): Promise<void> {
  try {
    await fetch(`${BASE_URL}/api/auth/logout`, {
      method: 'POST',
      credentials: 'include',
    })
  } catch (err) {
    console.error('Falha ao realizar logout na API', err)
  } finally {
    clearAuthToken()
    clearApiKey()
  }
}

/**
 * Returns the auth headers to attach to HTTP requests.
 * Prefers JWT Bearer token; falls back to X-Api-Key se for chave bruta legada.
 */
export function getAuthHeaders(): Record<string, string> {
  const token = getAuthToken()
  if (token) {
    return { Authorization: `Bearer ${token}` }
  }
  const apiKey = getApiKey()
  // Se for uma chave legada (diferente do indicador 'admin'), anexa
  if (apiKey && apiKey !== 'admin') {
    return { 'X-Api-Key': apiKey }
  }
  return {}
}
