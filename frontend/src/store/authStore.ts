import { create } from 'zustand'
import { supabase } from '../lib/supabase'
import type { User } from '@supabase/supabase-js'

interface AuthState {
  user: User | null
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

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  token: null,
  apiKey: null,
  isAuthenticated: false,
  isLoading: true,

  setUser: (user, token) => {
    set((state) => {
      const hasApiKey = !!state.apiKey || !!localStorage.getItem('agentic_api_key')
      return { 
        user, 
        token, 
        isAuthenticated: !!user || hasApiKey,
        isLoading: false 
      }
    })
  },

  loginWithToken: (token) => {
    set({ token, isAuthenticated: true, isLoading: false })
  },

  loginWithApiKey: async (apiKey) => {
    try {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ apiKey }),
      })
      if (!res.ok) return false
      localStorage.setItem('agentic_api_key', apiKey)
      set({ apiKey, isAuthenticated: true, isLoading: false })
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
        set({ 
          user: session.user, 
          token: session.access_token, 
          isAuthenticated: true 
        })
      } else {
        const savedApiKey = localStorage.getItem('agentic_api_key')
        if (savedApiKey) {
          set({ apiKey: savedApiKey, isAuthenticated: true })
        } else {
          set({ user: null, token: null, isAuthenticated: false })
        }
      }
    } catch (error) {
      console.error('Error checking auth:', error)
      const savedApiKey = localStorage.getItem('agentic_api_key')
      if (savedApiKey) {
        set({ apiKey: savedApiKey, isAuthenticated: true })
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
    localStorage.removeItem('agentic_api_key')
    set({ user: null, token: null, apiKey: null, isAuthenticated: false })
  },
}))

// Setup listener
supabase.auth.onAuthStateChange((_event, session) => {
  useAuthStore.getState().setUser(session?.user ?? null, session?.access_token ?? null)
})
