import { useState } from 'react'
import type { FormEvent } from 'react'
import { Icon } from '@iconify/react'
import { toast } from 'react-toastify'
import { getErrorMessage } from '../../lib/api'
import { useAuth } from '../auth/auth-context'
import { saveActiveExamScheduleId } from './examSessionStorage'

function readPinFromUrl() {
  try {
    return new URLSearchParams(window.location.search).get('pin') ?? ''
  } catch {
    return ''
  }
}

export function JoinExamPage({ onJoined }: { onJoined: () => void }) {
  const { joinExam } = useAuth()
  const [pinCode, setPinCode] = useState(readPinFromUrl)
  const [participantName, setParticipantName] = useState('')
  const [consentAccepted, setConsentAccepted] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!consentAccepted) {
      toast.error('Trebuie să accepți termenii și prelucrarea datelor pentru a participa.')
      return
    }

    setSubmitting(true)

    try {
      const session = await joinExam(pinCode.trim(), participantName.trim(), consentAccepted)
      saveActiveExamScheduleId(session.examScheduleId)
      onJoined()
    } catch (error) {
      toast.error(getErrorMessage(error))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="min-h-dvh bg-slate-100 text-slate-950">
      <section className="mx-auto grid min-h-dvh w-full max-w-5xl items-center gap-8 px-4 py-8 md:grid-cols-[1.1fr_0.9fr]">
        <div className="space-y-5">
          <div className="flex items-center gap-3 text-sm font-semibold uppercase tracking-wide text-emerald-700">
            <Icon icon="mdi:shield-check" className="h-6 w-6" />
            Behavioral Anticheat Engine
          </div>
          <h1 className="max-w-3xl text-4xl font-semibold leading-tight text-slate-950 md:text-5xl">
            Join an exam
          </h1>
          <p className="max-w-xl text-lg leading-8 text-slate-700">
            Enter the access code your proctor shared with you (link, QR code, or PIN), then your name. No account needed.
          </p>

          <details className="max-w-xl rounded-md border border-slate-200 bg-white p-3 text-sm text-slate-700">
            <summary className="cursor-pointer select-none font-medium text-slate-900">
              Termeni de participare și prelucrarea datelor (click pentru a citi)
            </summary>
            <div className="mt-2 space-y-2 text-xs leading-relaxed text-slate-600">
              <p>
                Această platformă este dezvoltată în cadrul unei lucrări de masterat ("Behavioral Anticheat Engine") și este folosită atât
                pentru desfășurarea testului, cât și pentru colectarea de date în scop de cercetare academică.
              </p>
              <p>Prin participarea la acest test, confirmi că ai citit și ești de acord cu următoarele:</p>
              <ul className="list-disc space-y-1 pl-4">
                <li>
                  Se colectează: numele introdus, răspunsurile la întrebări, și evenimente comportamentale din timpul testului (schimbarea
                  ferestrei/tab-ului, copy/paste, ieșire din fullscreen, folosirea devtools, viteza de tastare, perioade de inactivitate), precum
                  și un răspuns opțional de auto-raportare la finalul testului.
                </li>
                <li>Scopul colectării este dublu: evaluarea academică (nota) și cercetarea privind detecția comportamentală a fraudei, pentru teza de masterat.</li>
                <li>Datele sunt accesibile doar coordonatorului testului (profesor/proctor) și autorului tezei.</li>
                <li>Participarea la colectarea de date pentru cercetare este voluntară și nu influențează nota obținută la test.</li>
                <li>
                  Datele sunt prelucrate conform Legii nr. 133/2011 privind protecția datelor cu caracter personal (Republica Moldova). Ai dreptul
                  de acces, rectificare, ștergere a datelor și de retragere a consimțământului, contactând coordonatorul testului.
                </li>
              </ul>
            </div>
          </details>
        </div>

        <form onSubmit={(event) => void handleSubmit(event)} className="rounded-lg border border-slate-300 bg-white p-5 shadow-sm">
          <label className="mb-4 block text-sm font-medium text-slate-700">
            Access PIN
            <input
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-3 text-center text-2xl font-bold tracking-[0.3em] text-slate-950 outline-none focus:border-emerald-600"
              inputMode="numeric"
              maxLength={6}
              value={pinCode}
              onChange={(event) => setPinCode(event.target.value.replace(/\D/g, '').slice(0, 6))}
              placeholder="000000"
              required
            />
          </label>

          <label className="mb-4 block text-sm font-medium text-slate-700">
            Your name
            <input
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-slate-950 outline-none focus:border-emerald-600"
              value={participantName}
              onChange={(event) => setParticipantName(event.target.value)}
              placeholder="e.g. Ana Popescu"
              maxLength={120}
              required
            />
          </label>

          <label className="mb-4 flex items-start gap-2 text-sm text-slate-700">
            <input
              type="checkbox"
              className="mt-0.5 h-4 w-4 shrink-0 accent-emerald-700"
              checked={consentAccepted}
              onChange={(event) => setConsentAccepted(event.target.checked)}
              required
            />
            <span>
              Am citit și sunt de acord cu termenii de participare și cu prelucrarea datelor mele în scop de cercetare academică, conform
              celor descrise mai sus.
            </span>
          </label>

          <button
            type="submit"
            disabled={submitting || !consentAccepted}
            className="inline-flex w-full items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 py-2.5 font-semibold text-white hover:bg-emerald-800 disabled:cursor-not-allowed disabled:bg-slate-400"
          >
            <Icon icon={submitting ? 'mdi:loading' : 'mdi:login-variant'} className={`h-5 w-5 ${submitting ? 'animate-spin' : ''}`} />
            {submitting ? 'Joining...' : 'Join exam'}
          </button>
        </form>
      </section>
    </main>
  )
}
