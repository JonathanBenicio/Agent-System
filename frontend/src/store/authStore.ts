import { create } from 'zustand'
import {
  getAuthToken,
  setAuthToken,
  clearAuthToken,
  getApiKey,
  clearApiKey,
  loginWithApiKeyApi,
  logoutApi,
} from '../lib/auth'

interface AuthState {
  token: string | null
  apiKey: string | null
  isAuthenticated: boolean
  loginWithToken: (token: string) => void
  loginWithApiKey: (apiKey: string) => Promise<boolean>
  logout: () => Promise<void>
}

export const useAuthStore = create<AuthState>((set) => {
  const initialToken = getAuthToken()
  const initialApiKey = getApiKey()

  return {
    token: initialToken,
    apiKey: initialApiKey,
    isAuthenticated: Boolean(initialToken || initialApiKey),

    loginWithToken: (token: string) => {
      setAuthToken(token)
      set({ token, apiKey: null, isAuthenticated: true })
      clearApiKey()
    },

    loginWithApiKey: async (apiKey: string) => {
      const success = await loginWithApiKeyApi(apiKey)
      if (success) {
        set({ apiKey: 'admin', token: null, isAuthenticated: true })
        clearAuthToken()
        return true
      }
      return false
    },

    logout: async () => {
      await logoutApi()
      set({ token: null, apiKey: null, isAuthenticated: false })
    },
  }
})

if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === 'agentic_auth_token' || event.key === 'agentic_api_key') {
      const newToken = getAuthToken()
      const newApiKey = getApiKey()
      useAuthStore.setState({
        token: newToken,
        apiKey: newApiKey,
        isAuthenticated: Boolean(newToken || newApiKey),
      })
    }
  })
}
