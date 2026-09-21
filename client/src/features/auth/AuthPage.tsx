import { useState } from 'react'
import type { FormEvent } from 'react'
import { Icon } from '@iconify/react'
import { toast } from 'react-toastify'
import { getErrorMessage, type Role } from '../../lib/api'
import { useAuth } from './auth-context'

type AuthMode = 'login' | 'signup'

export function AuthPage({ onBackToJoin }: { onBackToJoin: () => void }) {
  const { login, signUp } = useAuth()
  const [mode, setMode] = useState<AuthMode>('login')
  const [email, setEmail] = useState('admin@example.test')
  const [password, setPassword] = useState('Admin123!')
  const [role] = useState<Exclude<Role, 'Admin'>>('Proctor')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)

    try {
      if (mode === 'login') {
        await login(email, password)
      } else {
        await signUp(email, password, role)
      }
    } catch (error) {
      toast.error(getErrorMessage(error))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="min-h-dvh bg-slate-100 text-slate-950">
      <section className="mx-auto grid min-h-dvh w-full max-w-6xl items-center gap-8 px-4 py-8 md:grid-cols-[1.1fr_0.9fr]">
        <div className="space-y-5">
          <div className="flex items-center gap-3 text-sm font-semibold uppercase tracking-wide text-emerald-700">
            <Icon icon="mdi:shield-check" className="h-6 w-6" />
            Behavioral Anticheat Engine
          </div>
          <h1 className="max-w-3xl text-4xl font-semibold leading-tight text-slate-950 md:text-6xl">
            Online examination with behavioral audit trails
          </h1>
          <p className="max-w-2xl text-lg leading-8 text-slate-700">
            Run exams, collect signed interaction events, and inspect live risk signals without video or audio proctoring.
          </p>
          <div className="grid gap-3 text-sm text-slate-700 sm:grid-cols-3">
            {['JWT roles', 'HMAC envelopes', 'Live dashboard'].map((item) => (
              <div key={item} className="rounded-md border border-slate-300 bg-white px-3 py-2">
                {item}
              </div>
            ))}
          </div>
        </div>

        <form onSubmit={handleSubmit} className="rounded-lg border border-slate-300 bg-white p-5 shadow-sm">
          <div className="mb-5 flex rounded-md bg-slate-100 p-1 text-sm font-medium">
            <button
              type="button"
              className={`flex-1 rounded px-3 py-2 ${mode === 'login' ? 'bg-white text-slate-950 shadow-sm' : 'text-slate-600'}`}
              onClick={() => setMode('login')}
            >
              Login
            </button>
            <button
              type="button"
              className={`flex-1 rounded px-3 py-2 ${mode === 'signup' ? 'bg-white text-slate-950 shadow-sm' : 'text-slate-600'}`}
              onClick={() => setMode('signup')}
            >
              Sign up
            </button>
          </div>

          <label className="mb-4 block text-sm font-medium text-slate-700">
            Email
            <input
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-slate-950 outline-none focus:border-emerald-600"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </label>

          <label className="mb-4 block text-sm font-medium text-slate-700">
            Password
            <input
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-slate-950 outline-none focus:border-emerald-600"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          </label>

          {mode === 'signup' ? (
            <p className="mb-4 text-xs text-slate-500">New accounts are created with the Proctor role. Admin accounts are seeded separately.</p>
          ) : null}

          <button
            type="submit"
            disabled={submitting}
            className="inline-flex w-full items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 py-2.5 font-semibold text-white hover:bg-emerald-800 disabled:cursor-not-allowed disabled:bg-slate-400"
          >
            <Icon icon={submitting ? 'mdi:loading' : 'mdi:login'} className={`h-5 w-5 ${submitting ? 'animate-spin' : ''}`} />
            {mode === 'login' ? 'Login' : 'Create account'}
          </button>

          <button
            type="button"
            onClick={onBackToJoin}
            className="mt-4 w-full text-center text-xs font-medium text-slate-500 underline-offset-2 hover:underline"
          >
            Back to join an exam
          </button>
        </form>
      </section>
    </main>
  )
}
