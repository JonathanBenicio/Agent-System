import { createClient } from '@supabase/supabase-js'

const supabaseUrl = import.meta.env.VITE_SUPABASE_URL
const supabaseAnonKey = import.meta.env.VITE_SUPABASE_ANON_KEY

if (!supabaseUrl || !supabaseAnonKey) {
  console.warn('⚠️ Supabase credentials missing in environment variables. OAuth login will not work. Using dummy credentials to prevent crashes.')
}

// Fallback to dummy URL to prevent createClient from throwing an error.
const safeUrl = supabaseUrl || 'https://dummy-project.supabase.co'
const safeKey = supabaseAnonKey || 'dummy-anon-key'

export const supabase = createClient(safeUrl, safeKey)
