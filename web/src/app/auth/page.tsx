'use client'

import { useState } from 'react'
import { supabase } from '@/lib/supabase'
import { useRouter } from 'next/navigation'
import { Swords, Mail, Lock, LogIn, UserPlus } from 'lucide-react'

type AuthMode = 'signin' | 'signup'

export default function AuthPage() {
  const router = useRouter()
  const [mode, setMode] = useState<AuthMode>('signin')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    setLoading(true)

    try {
      if (mode === 'signin') {
        const { error } = await supabase.auth.signInWithPassword({
          email,
          password,
        })
        if (error) throw error
        router.push('/')
      } else {
        const { error } = await supabase.auth.signUp({
          email,
          password,
        })
        if (error) throw error
        setSuccess('Check your email for a confirmation link to complete your signup.')
      }
    } catch (err: unknown) {
      if (err instanceof Error) {
        setError(err.message)
      } else {
        setError('An unexpected error occurred.')
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center px-4">
      <div className="w-full max-w-md">
        {/* Glass card */}
        <div
          className="rounded-2xl border border-[var(--border-subtle)] bg-[var(--bg-secondary)]/80 backdrop-blur-xl shadow-2xl overflow-hidden"
        >
          {/* Gold accent border on top */}
          <div className="h-1 bg-gradient-to-r from-amber-600 via-amber-500 to-amber-600" />

          <div className="p-8">
            {/* Logo */}
            <div className="flex flex-col items-center mb-8">
              <div className="w-16 h-16 rounded-full bg-amber-600/20 border border-amber-600/30 flex items-center justify-center mb-4">
                <Swords className="w-8 h-8 text-amber-500" />
              </div>
              <h1 className="text-2xl font-bold text-[var(--text-primary)]">
                Slay the Stats
              </h1>
              <p className="text-sm text-[var(--text-subtle)] mt-1">
                Track your runs. Master the Spire.
              </p>
            </div>

            {/* Tabs */}
            <div className="flex mb-6 border-b border-[var(--border-subtle)]">
              <button
                type="button"
                onClick={() => {
                  setMode('signin')
                  setError(null)
                  setSuccess(null)
                }}
                className={`flex-1 pb-3 text-sm font-medium transition-colors cursor-pointer ${
                  mode === 'signin'
                    ? 'text-amber-500 border-b-2 border-amber-500'
                    : 'text-[var(--text-subtle)] hover:text-[var(--text-secondary)]'
                }`}
              >
                <span className="inline-flex items-center gap-1.5">
                  <LogIn className="w-4 h-4" />
                  Sign In
                </span>
              </button>
              <button
                type="button"
                onClick={() => {
                  setMode('signup')
                  setError(null)
                  setSuccess(null)
                }}
                className={`flex-1 pb-3 text-sm font-medium transition-colors cursor-pointer ${
                  mode === 'signup'
                    ? 'text-amber-500 border-b-2 border-amber-500'
                    : 'text-[var(--text-subtle)] hover:text-[var(--text-secondary)]'
                }`}
              >
                <span className="inline-flex items-center gap-1.5">
                  <UserPlus className="w-4 h-4" />
                  Sign Up
                </span>
              </button>
            </div>

            {/* Form */}
            <form onSubmit={handleSubmit} className="space-y-4">
              {/* Email */}
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--text-subtle)]" />
                <input
                  type="email"
                  placeholder="Email address"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  className="w-full pl-10 pr-4 py-2.5 bg-[var(--bg-primary)] border border-[var(--border-subtle)] rounded-[var(--radius-md)] text-[var(--text-primary)] placeholder:text-[var(--text-subtle)] focus:border-[var(--gold)]/50 focus:outline-none transition-colors"
                />
              </div>

              {/* Password */}
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[var(--text-subtle)]" />
                <input
                  type="password"
                  placeholder="Password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  minLength={6}
                  className="w-full pl-10 pr-4 py-2.5 bg-[var(--bg-primary)] border border-[var(--border-subtle)] rounded-[var(--radius-md)] text-[var(--text-primary)] placeholder:text-[var(--text-subtle)] focus:border-[var(--gold)]/50 focus:outline-none transition-colors"
                />
              </div>

              {/* Error message */}
              {error && (
                <div className="text-sm text-red-400 bg-red-400/10 border border-red-400/20 rounded-[var(--radius-md)] px-3 py-2">
                  {error}
                </div>
              )}

              {/* Success message */}
              {success && (
                <div className="text-sm text-green-400 bg-green-400/10 border border-green-400/20 rounded-[var(--radius-md)] px-3 py-2">
                  {success}
                </div>
              )}

              {/* Submit button */}
              <button
                type="submit"
                disabled={loading}
                className="w-full py-2.5 bg-gradient-to-r from-amber-600 to-amber-700 hover:from-amber-500 hover:to-amber-600 text-white font-semibold rounded-[var(--radius-md)] transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {loading
                  ? 'Please wait...'
                  : mode === 'signin'
                    ? 'Sign In'
                    : 'Create Account'}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}
