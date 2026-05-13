import { create } from 'zustand'
import {
  getAuthToken,
  setAuthToken,
  clearAuthToken,
  getApiKey,
  setApiKey,
  clearApiKey,
} from '../lib/auth'

interface AuthState {
  token: string | null
  apiKey: string | null
  isAuthenticated: boolean
  loginWithToken: (token: string) => void
  loginWithApiKey: (apiKey: string) => void
  logout: () => void
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

    loginWithApiKey: (apiKey: string) => {
      setApiKey(apiKey)
      set({ apiKey, token: null, isAuthenticated: true })
      clearAuthToken()
    },

    logout: () => {
      clearAuthToken()
      clearApiKey()
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
