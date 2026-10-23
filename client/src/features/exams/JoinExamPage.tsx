import { useState } from 'react'
import type { FormEvent } from 'react'
import { Icon } from '@iconify/react'
import { toast } from 'react-toastify'
import { getErrorMessage } from '../../lib/api'
import { useAuth } from '../auth/auth-context'
import { useLanguage } from '../../lib/i18n/language-context'
import { LanguageSwitcher } from '../../lib/i18n/LanguageSwitcher'
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
  const { t } = useLanguage()
  const [pinCode, setPinCode] = useState(readPinFromUrl)
  const [participantName, setParticipantName] = useState('')
  const [consentAccepted, setConsentAccepted] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!consentAccepted) {
      toast.error(t.joinExam.consentRequiredError)
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
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-3 text-sm font-semibold uppercase tracking-wide text-emerald-700">
              <Icon icon="mdi:shield-check" className="h-6 w-6" />
              {t.common.appName}
            </div>
            <LanguageSwitcher />
          </div>
          <h1 className="max-w-3xl text-4xl font-semibold leading-tight text-slate-950 md:text-5xl">
            {t.joinExam.heroTitle}
          </h1>
          <p className="max-w-xl text-lg leading-8 text-slate-700">
            {t.joinExam.heroSubtitle}
          </p>

          <details className="max-w-xl rounded-md border border-slate-200 bg-white p-3 text-sm text-slate-700">
            <summary className="cursor-pointer select-none font-medium text-slate-900">
              {t.joinExam.consentSummary}
            </summary>
            <div className="mt-2 space-y-2 text-xs leading-relaxed text-slate-600">
              <p>{t.joinExam.consentIntro1}</p>
              <p>{t.joinExam.consentIntro2}</p>
              <ul className="list-disc space-y-1 pl-4">
                {t.joinExam.consentItems.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          </details>
        </div>

        <form onSubmit={(event) => void handleSubmit(event)} className="rounded-lg border border-slate-300 bg-white p-5 shadow-sm">
          <label className="mb-4 block text-sm font-medium text-slate-700">
            {t.joinExam.accessPinLabel}
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
            {t.joinExam.nameLabel}
            <input
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-slate-950 outline-none focus:border-emerald-600"
              value={participantName}
              onChange={(event) => setParticipantName(event.target.value)}
              placeholder={t.joinExam.namePlaceholder}
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
            <span>{t.joinExam.consentCheckboxLabel}</span>
          </label>

          <button
            type="submit"
            disabled={submitting || !consentAccepted}
            className="inline-flex w-full items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 py-2.5 font-semibold text-white hover:bg-emerald-800 disabled:cursor-not-allowed disabled:bg-slate-400"
          >
            <Icon icon={submitting ? 'mdi:loading' : 'mdi:login-variant'} className={`h-5 w-5 ${submitting ? 'animate-spin' : ''}`} />
            {submitting ? t.joinExam.joiningButton : t.joinExam.joinButton}
          </button>
        </form>
      </section>
    </main>
  )
}
