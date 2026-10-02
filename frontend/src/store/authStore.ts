import { create } from 'zustand'
import { supabase } from '../lib/supabase'
import type { User } from '@supabase/supabase-js'
import { useKnowledgeStore } from './useKnowledgeStore'
import {
  clearAuthToken, setAuthToken, clearLegacyApiKey,
  getCookieSession, loginWithApiKeyApi, logoutApi,
} from '../lib/auth'

interface AuthState {
  user: User | null
  principalId: string | null
  token: string | null
  cookieAuthenticated: boolean
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
    const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
    const payload = decodeURIComponent(
      atob(base64).split('').map(c => '%' + c.charCodeAt(0).toString(16).padStart(2, '0')).join('')
    )
    return JSON.parse(payload)
  } catch {
    return null
  }
}

clearLegacyApiKey()

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  principalId: null,
  token: null,
  cookieAuthenticated: false,
  isAuthenticated: false,
  isLoading: true,

  setUser: (user, token) => {
    // Initial Supabase null events must not discard independently authenticated modes.
    if (!user && !token && (get().cookieAuthenticated || (get().token && !get().user))) return
    if (token) {
      setAuthToken(token)
      clearLegacyApiKey()
      const claims = parseJwt(token)
      const tenantId = claims?.tenant_id || claims?.app_metadata?.tenant_id
      if (tenantId) useKnowledgeStore.getState().setActiveWorkspace(tenantId)
    }
    const claims = token ? parseJwt(token) : null
    set({
      user, token, cookieAuthenticated: false,
      principalId: user?.id ?? claims?.nameid ?? claims?.sub ?? null,
      isAuthenticated: !!user || !!token,
      isLoading: false,
    })
  },

  loginWithToken: (token) => {
    setAuthToken(token)
    clearLegacyApiKey()
    const claims = parseJwt(token)
    const tenantId = claims?.tenant_id || claims?.app_metadata?.tenant_id
    if (tenantId) useKnowledgeStore.getState().setActiveWorkspace(tenantId)
    set({
      user: null, token, principalId: claims?.nameid ?? claims?.sub ?? null,
      cookieAuthenticated: false, isAuthenticated: true, isLoading: false,
    })
  },

  loginWithApiKey: async (apiKey) => {
    try {
      await supabase.auth.signOut({ scope: 'local' })
      const session = await loginWithApiKeyApi(apiKey)
      if (!session) return false
      clearAuthToken()
      useKnowledgeStore.getState().setActiveWorkspace(session.tenantId)
      set({
        user: null, token: null, principalId: session.userId,
        cookieAuthenticated: true, isAuthenticated: true, isLoading: false,
      })
      return true
    } catch {
      return false
    }
  },

  checkAuth: async () => {
    set({ isLoading: true })
    clearLegacyApiKey()
    try {
      // Supabase is optional in deployments authenticating with API-key cookies.
      const result = await supabase.auth.getSession().catch(() => null)
      if (result?.data.session) {
        get().setUser(result.data.session.user, result.data.session.access_token)
        return
      }
      const savedToken = localStorage.getItem('agentic_auth_token')
      if (savedToken && parseJwt(savedToken)?.exp > Math.floor(Date.now() / 1000)) {
        get().loginWithToken(savedToken)
        return
      }
      clearAuthToken()
      const session = await getCookieSession()
      if (session) {
        useKnowledgeStore.getState().setActiveWorkspace(session.tenantId)
        set({
          user: null, token: null, principalId: session.userId,
          cookieAuthenticated: true, isAuthenticated: true,
        })
      } else {
        set({ user: null, principalId: null, token: null, cookieAuthenticated: false, isAuthenticated: false })
      }
    } catch {
      set({ user: null, principalId: null, token: null, cookieAuthenticated: false, isAuthenticated: false })
    } finally {
      set({ isLoading: false })
    }
  },

  login: async () => {
    await supabase.auth.signInWithOAuth({ provider: 'google' })
  },

  logout: async () => {
    // Do not report logout success while the HttpOnly cookie could still authenticate.
    await logoutApi()
    await supabase.auth.signOut({ scope: 'local' })
    clearAuthToken()
    clearLegacyApiKey()
    useKnowledgeStore.getState().setActiveWorkspace('')
    set({ user: null, principalId: null, token: null, cookieAuthenticated: false, isAuthenticated: false })
  },
}))

supabase.auth.onAuthStateChange((_event, session) => {
  useAuthStore.getState().setUser(session?.user ?? null, session?.access_token ?? null)
})
