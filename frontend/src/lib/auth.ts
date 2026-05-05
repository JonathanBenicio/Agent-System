// ══════════════════════════════════════
// Auth token management
// ══════════════════════════════════════

const TOKEN_KEY = 'agentic_auth_token'
const API_KEY_KEY = 'agentic_api_key'

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
 * Returns the auth headers to attach to HTTP requests.
 * Prefers JWT Bearer token; falls back to X-Api-Key.
 */
export function getAuthHeaders(): Record<string, string> {
  const token = getAuthToken()
  if (token) {
    return { Authorization: `Bearer ${token}` }
  }
  const apiKey = getApiKey()
  if (apiKey) {
    return { 'X-Api-Key': apiKey }
  }
  return {}
}
