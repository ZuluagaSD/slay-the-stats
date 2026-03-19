import { createClient } from '@supabase/supabase-js'

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL || 'https://sskibxdluttejitksnkr.supabase.co'
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY || 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InNza2lieGRsdXR0ZWppdGtzbmtyIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzM5Mjc1NTEsImV4cCI6MjA4OTUwMzU1MX0.K-gYtPXHMGqKhTU3ARiJT9nIeiLJ3gHKlcwj-T9pyEk'

export const supabase = createClient(supabaseUrl, supabaseAnonKey)
