import { create } from 'zustand'
import { supabase } from '../lib/supabase'
import type { User } from '@supabase/supabase-js'
import { useKnowledgeStore } from './useKnowledgeStore'
import { clearAuthToken, setAuthToken } from '../lib/auth'

interface AuthState {
  user: User | null
  principalId: string | null
  token: string | null
  apiKey: string | null
  isAuthenticated: boolean
  isLoading: boolean
  checkAuth: () => Promise<void>
  login: () => Promise<void>
  loginWithToken: (token: string) => void
  loginWithApiKey: (apiKey: string) => Promise<boolean>
  logout: () => Promise<void>
  setUser: (user: User | null, token: string | null) => void
}

function parseJwt(token: string) {
  try {
    const base64Url = token.split('.')[1]
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/')
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload)
  } catch {
    return null
  }
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  principalId: null,
  token: null,
  apiKey: null,
  isAuthenticated: false,
  isLoading: true,

  setUser: (user, token) => {
    // Supabase emits an initial null session even when the user authenticated with a standalone JWT.
    // Keep that independently managed bearer token until the explicit logout action clears it.
    if (!user && !token && get().token && !get().user) return
    if (token) {
      setAuthToken(token)
      localStorage.removeItem('agentic_api_key')
      localStorage.removeItem('agentic_api_key_subject')
      const decoded = parseJwt(token)
      const tenantId = decoded?.tenant_id || decoded?.app_metadata?.tenant_id
      if (tenantId) {
        useKnowledgeStore.getState().setActiveWorkspace(tenantId)
      }
    }
    set((state) => {
      const hasApiKey = !!state.apiKey || !!localStorage.getItem('agentic_api_key')
      const decoded = token ? parseJwt(token) : null
      return { 
        user, 
        token, 
        apiKey: token ? null : state.apiKey,
        principalId: user?.id ?? decoded?.nameid ?? decoded?.sub ?? null,
        isAuthenticated: !!user || hasApiKey,
        isLoading: false 
      }
    })
  },

  loginWithToken: (token) => {
    let principalId: string | null = null
    if (token) {
      setAuthToken(token)
      localStorage.removeItem('agentic_api_key')
      localStorage.removeItem('agentic_api_key_subject')
      const decoded = parseJwt(token)
      principalId = decoded?.nameid ?? decoded?.sub ?? null
      const tenantId = decoded?.tenant_id || decoded?.app_metadata?.tenant_id
      if (tenantId) {
        useKnowledgeStore.getState().setActiveWorkspace(tenantId)
      }
    }
    set({ token, principalId, apiKey: null, isAuthenticated: true, isLoading: false })
  },

  loginWithApiKey: async (apiKey) => {
    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ apiKey }),
      })
      if (!res.ok) return false
      const data = await res.json()
      const tenantId = data.tenantId
      if (tenantId) {
        useKnowledgeStore.getState().setActiveWorkspace(tenantId)
      }
      clearAuthToken()
      localStorage.setItem('agentic_api_key', apiKey)
      localStorage.setItem('agentic_api_key_subject', data.userId)
      set({ apiKey, principalId: data.userId, token: null, isAuthenticated: true, isLoading: false })
      return true
    } catch {
      return false
    }
  },

  checkAuth: async () => {
    set({ isLoading: true })
    try {
      const { data: { session } } = await supabase.auth.getSession()
      if (session) {
        setAuthToken(session.access_token)
        set({ 
          user: session.user, 
          principalId: session.user.id,
          token: session.access_token, 
          isAuthenticated: true 
        })
        const decoded = parseJwt(session.access_token)
        const tenantId = decoded?.tenant_id || decoded?.app_metadata?.tenant_id
        if (tenantId) {
          useKnowledgeStore.getState().setActiveWorkspace(tenantId)
        }
      } else {
        const savedToken = localStorage.getItem('agentic_auth_token')
        const savedClaims = savedToken ? parseJwt(savedToken) : null
        if (savedToken && savedClaims?.exp > Math.floor(Date.now() / 1000)) {
          const principalId = savedClaims.nameid ?? savedClaims.sub ?? null
          const tenantId = savedClaims.tenant_id || savedClaims.app_metadata?.tenant_id
          if (tenantId) useKnowledgeStore.getState().setActiveWorkspace(tenantId)
          set({ token: savedToken, principalId, apiKey: null, isAuthenticated: true })
          return
        }
        clearAuthToken()
        const savedApiKey = localStorage.getItem('agentic_api_key')
        if (savedApiKey) {
          set({ apiKey: savedApiKey, principalId: localStorage.getItem('agentic_api_key_subject'), isAuthenticated: true })
        } else {
          set({ user: null, token: null, isAuthenticated: false })
        }
      }
    } catch (error) {
      console.error('Error checking auth:', error)
      const savedToken = localStorage.getItem('agentic_auth_token')
      const savedClaims = savedToken ? parseJwt(savedToken) : null
      if (savedToken && savedClaims?.exp > Math.floor(Date.now() / 1000)) {
        const principalId = savedClaims.nameid ?? savedClaims.sub ?? null
        const tenantId = savedClaims.tenant_id || savedClaims.app_metadata?.tenant_id
        if (tenantId) useKnowledgeStore.getState().setActiveWorkspace(tenantId)
        set({ token: savedToken, principalId, apiKey: null, isAuthenticated: true })
        return
      }
      clearAuthToken()
      const savedApiKey = localStorage.getItem('agentic_api_key')
      if (savedApiKey) {
        set({ apiKey: savedApiKey, principalId: localStorage.getItem('agentic_api_key_subject'), isAuthenticated: true })
      } else {
        set({ user: null, token: null, isAuthenticated: false })
      }
    } finally {
      set({ isLoading: false })
    }
  },

  login: async () => {
    await supabase.auth.signInWithOAuth({ provider: 'google' })
  },

  logout: async () => {
    await supabase.auth.signOut()
    clearAuthToken()
    localStorage.removeItem('agentic_api_key')
    localStorage.removeItem('agentic_api_key_subject')
    useKnowledgeStore.getState().setActiveWorkspace('')
    set({ user: null, principalId: null, token: null, apiKey: null, isAuthenticated: false })
  },
}))

// Setup listener
supabase.auth.onAuthStateChange((_event, session) => {
  useAuthStore.getState().setUser(session?.user ?? null, session?.access_token ?? null)
})
