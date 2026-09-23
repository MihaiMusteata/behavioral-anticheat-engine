import { useCallback, useEffect, useRef, useState } from 'react'
import { Icon } from '@iconify/react'
import { toast } from 'react-toastify'
import { api, getErrorMessage } from '../../lib/api'
import { useBehaviorCollector } from '../behavior/useBehaviorCollector'
import { useAuth } from '../auth/auth-context'
import { Shell } from '../../shared/Shell'
import {
  clearActiveExamScheduleId,
  clearActiveQuestionIndex,
  readActiveExamScheduleId,
  readActiveQuestionIndex,
  saveActiveQuestionIndex,
} from './examSessionStorage'
import type { ExistingAnswer, Question, StartExamSessionResponse, SubmitExamResponse } from './types'

type AnswerValue = string | string[]

export function StudentApp() {
  const { restoredFromStorage, logout } = useAuth()
  const [session, setSession] = useState<StartExamSessionResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const resumeAttemptedRef = useRef(false)

  useEffect(() => {
    if (resumeAttemptedRef.current) {
      return
    }
    resumeAttemptedRef.current = true

    const examScheduleId = readActiveExamScheduleId()
    if (!examScheduleId) {
      toast.error('No exam reference found for this account.')
      clearActiveExamScheduleId()
      clearActiveQuestionIndex()
      logout()
      return
    }

    void api
      .post<StartExamSessionResponse>(`/api/student/exams/${examScheduleId}/start`)
      .then((response) => {
        setSession(response.data)
      })
      .catch((error: unknown) => {
        toast.error(getErrorMessage(error))
        clearActiveExamScheduleId()
        clearActiveQuestionIndex()
        logout()
      })
      .finally(() => setLoading(false))
  }, [logout])

  if (loading) {
    return (
      <Shell title="Exam">
        <main className="mx-auto max-w-3xl px-4 py-10 text-center text-slate-600">Loading your exam session...</main>
      </Shell>
    )
  }

  if (!session) {
    return null
  }

  return (
    <Shell title="Exam">
      <main className="mx-auto max-w-7xl px-4 py-6">
        <ExamRunner
          session={session}
          isRestored={restoredFromStorage}
          onFinished={() => {
            clearActiveExamScheduleId()
            clearActiveQuestionIndex()
            setSession(null)
            logout()
          }}
        />
      </main>
    </Shell>
  )
}

function ExamRunner({
  session,
  isRestored,
  onFinished,
}: {
  session: StartExamSessionResponse
  isRestored: boolean
  onFinished: () => void
}) {
  const [currentIndex, setCurrentIndex] = useState(() => {
    const storedIndex = readActiveQuestionIndex(session.sessionId)
    return storedIndex !== null && storedIndex >= 0 && storedIndex < session.assessment.questions.length
      ? storedIndex
      : 0
  })
  const [answers, setAnswers] = useState<Record<string, AnswerValue>>(() => hydrateAnswers(session.assessment.questions, session.existingAnswers))
  const [saving, setSaving] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [showSelfReportModal, setShowSelfReportModal] = useState(false)
  const [selfReportSubmitting, setSelfReportSubmitting] = useState(false)
  const [finalScore, setFinalScore] = useState<number | null>(null)
  const [showResult, setShowResult] = useState(false)
  const [questionViewGeneration, setQuestionViewGeneration] = useState(0)
  const [now, setNow] = useState(Date.now())
  const behavior = useBehaviorCollector(
    session.sessionId,
    session.behaviorSigningSecret,
    !submitted,
    isRestored ? 'restored_from_storage' : null,
  )
  const answerStats = useRef<Record<string, { changes: number; lastLength: number; lastAt: number; sampleLength: number; sampleAt: number }>>({})
  const activeQuestionViewRef = useRef<{ questionId: string; questionIndex: number; orderIndex: number; startedAt: number } | null>(null)
  const submitInProgressRef = useRef(false)
  const submittedRef = useRef(false)
  const question = session.assessment.questions[currentIndex]
  const remainingMs = Math.max(new Date(session.endsAtUtc).getTime() - now, 0)

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    function handleBeforeUnload() {
      if (submittedRef.current) {
        return
      }

      behavior.record('session_end', { reason: 'window_closed', questionId: null, questionIndex: null })
      void behavior.flush(true)
    }

    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [behavior])

  const endActiveQuestionView = useCallback(() => {
    const activeView = activeQuestionViewRef.current
    if (!activeView) {
      return
    }

    activeQuestionViewRef.current = null
    behavior.record('question_view_end', {
      questionId: activeView.questionId,
      questionIndex: activeView.questionIndex,
      orderIndex: activeView.orderIndex,
      durationMs: Date.now() - activeView.startedAt,
    })
  }, [behavior])

  useEffect(() => {
    const startTimer = window.setTimeout(() => {
      const startedAt = Date.now()
      activeQuestionViewRef.current = {
        questionId: question.id,
        questionIndex: currentIndex,
        orderIndex: question.orderIndex,
        startedAt,
      }
      behavior.setContext({ questionId: question.id, questionIndex: currentIndex })
      behavior.record('question_view_start', {
        questionId: question.id,
        questionIndex: currentIndex,
        orderIndex: question.orderIndex,
      })
    }, 0)

    return () => {
      window.clearTimeout(startTimer)
      if (activeQuestionViewRef.current?.questionId === question.id) {
        endActiveQuestionView()
      }
    }
  }, [behavior, currentIndex, endActiveQuestionView, question.id, question.orderIndex, questionViewGeneration])

  const saveCurrentAnswer = useCallback(async () => {
    const answer = answers[question.id]
    if (!isAnswered(question, answer)) {
      toast.warn('Answer the current question before continuing.')
      return false
    }

    setSaving(true)
    try {
      await api.put(`/api/student/sessions/${session.sessionId}/answers`, {
        questionId: question.id,
        answer: toAnswerPayload(question, answer),
      })
      behavior.record('answer_saved', { questionId: question.id })
      return true
    } catch (error) {
      toast.error(getErrorMessage(error))
      return false
    } finally {
      setSaving(false)
    }
  }, [answers, behavior, question, session.sessionId])

  const persistCurrentAnswerIfPresent = useCallback(async () => {
    if (!isAnswered(question, answers[question.id])) {
      return
    }

    await saveCurrentAnswer()
  }, [answers, question, saveCurrentAnswer])

  async function goNext() {
    const saved = await saveCurrentAnswer()
    if (saved) {
      navigateTo(Math.min(currentIndex + 1, session.assessment.questions.length - 1), 'next_button')
    }
  }

  async function jumpToQuestion(nextIndex: number) {
    await persistCurrentAnswerIfPresent()
    navigateTo(nextIndex, 'question_button')
  }

  function navigateTo(nextIndex: number, source: string) {
    if (nextIndex === currentIndex) {
      return
    }

    const nextQuestion = session.assessment.questions[nextIndex]
    behavior.record('question_navigation', {
      fromQuestionId: question.id,
      toQuestionId: nextQuestion.id,
      fromIndex: currentIndex,
      toIndex: nextIndex,
      direction: nextIndex > currentIndex ? 'forward' : 'backward',
      source,
    })
    setCurrentIndex(nextIndex)
    saveActiveQuestionIndex(session.sessionId, nextIndex)
  }

  function updateAnswer(value: AnswerValue) {
    const previous = answers[question.id]
    setAnswers((current) => ({ ...current, [question.id]: value }))

    const stats = answerStats.current[question.id] ?? {
      changes: 0,
      lastLength: getAnswerLength(previous),
      lastAt: Date.now(),
      sampleLength: getAnswerLength(previous),
      sampleAt: Date.now(),
    }
    const changedAt = Date.now()
    const answerLength = getAnswerLength(value)
    const previousLength = stats.lastLength
    const elapsedMs = Math.max(changedAt - stats.lastAt, 1)
    stats.changes += 1

    behavior.record('answer_change', {
      questionId: question.id,
      questionType: question.type,
      changeCount: stats.changes,
      answerLength,
      selectedCount: Array.isArray(value) ? value.length : null,
    })

    if (question.type === 'free_text') {
      const charDelta = answerLength - previousLength
      if (charDelta >= 20 && elapsedMs <= 800) {
        behavior.record('text_burst_detected', {
          questionId: question.id,
          charDelta,
          elapsedMs,
          answerLength,
        })
      }

      const sampleElapsedMs = changedAt - stats.sampleAt
      if (sampleElapsedMs >= 5000) {
        behavior.record('typing_speed_sample', {
          questionId: question.id,
          charsPerSecond: Math.max((answerLength - stats.sampleLength) / (sampleElapsedMs / 1000), 0),
          sampleMs: sampleElapsedMs,
          answerLength,
        })
        stats.sampleAt = changedAt
        stats.sampleLength = answerLength
      }
    }

    stats.lastAt = changedAt
    stats.lastLength = answerLength
    answerStats.current[question.id] = stats
  }

  const submit = useCallback(async () => {
    if (submittedRef.current || submitInProgressRef.current) {
      return
    }

    submitInProgressRef.current = true
    setSubmitting(true)
    const saved = isAnswered(question, answers[question.id]) ? await saveCurrentAnswer() : true
    if (!saved) {
      submitInProgressRef.current = false
      setSubmitting(false)
      return
    }

    try {
      endActiveQuestionView()
      await behavior.endSession('submit')
      const response = await api.post<SubmitExamResponse>(`/api/student/sessions/${session.sessionId}/submit`)
      setFinalScore(response.data.score)
      submittedRef.current = true
      setSubmitted(true)
      behavior.clearSessionState()
      toast.success('Exam submitted.')
      setShowSelfReportModal(true)
    } catch (error) {
      behavior.record('session_resume', { reason: 'submit_failed', questionId: null, questionIndex: null })
      setQuestionViewGeneration((current) => current + 1)
      toast.error(getErrorMessage(error))
    } finally {
      submitInProgressRef.current = false
      setSubmitting(false)
    }
  }, [answers, behavior, endActiveQuestionView, question, saveCurrentAnswer, session.sessionId])

  useEffect(() => {
    if (remainingMs === 0 && !submitted) {
      void submit()
    }
  }, [remainingMs, submit, submitted])

  async function answerSelfReport(cheated: boolean) {
    setSelfReportSubmitting(true)
    try {
      await api.put(`/api/student/sessions/${session.sessionId}/self-report`, { cheated })
    } catch (error) {
      toast.error(getErrorMessage(error))
    } finally {
      setSelfReportSubmitting(false)
      setShowSelfReportModal(false)
      setShowResult(true)
    }
  }

  const totalPoints = session.assessment.questions.reduce((sum, item) => sum + item.points, 0)

  return (
    <section className="grid gap-4 lg:grid-cols-[260px_1fr]">
      <aside className="rounded-lg border border-slate-300 bg-white p-4">
        <div className="mb-4">
          <h2 className="font-semibold">{session.assessment.title}</h2>
          <p className="text-sm text-slate-600">{session.participantName}</p>
        </div>
        <CountdownClock remainingMs={remainingMs} />
        <div className="grid grid-cols-5 gap-2 lg:grid-cols-3">
          {session.assessment.questions.map((item, index) => (
            <button
              key={item.id}
              type="button"
              onClick={() => void jumpToQuestion(index)}
              className={`aspect-square rounded-md border text-sm font-semibold ${
                index === currentIndex
                  ? 'border-emerald-700 bg-emerald-700 text-white'
                  : isAnswered(item, answers[item.id])
                    ? 'border-emerald-300 bg-emerald-50 text-emerald-800'
                    : 'border-slate-300 bg-white text-slate-700'
              }`}
            >
              {index + 1}
            </button>
          ))}
        </div>
      </aside>

      <article className="rounded-lg border border-slate-300 bg-white p-5 shadow-sm">
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <p className="text-sm font-medium text-emerald-700">Question {currentIndex + 1} / {session.assessment.questions.length}</p>
            <h3 className="mt-1 text-xl font-semibold">{question.prompt}</h3>
          </div>
          <span className="rounded bg-slate-100 px-2 py-1 text-sm text-slate-700">{question.points} pts</span>
        </div>

        <QuestionAnswer
          question={question}
          value={answers[question.id]}
          onChange={updateAnswer}
        />

        <div className="mt-6 flex flex-wrap items-center justify-end gap-3">
          {saving ? (
            <span className="inline-flex items-center gap-1 text-xs font-medium text-slate-500">
              <Icon icon="mdi:loading" className="h-4 w-4 animate-spin" />
              Saving...
            </span>
          ) : null}
          {currentIndex < session.assessment.questions.length - 1 ? (
            <button
              type="button"
              onClick={() => void goNext()}
              className="inline-flex items-center gap-2 rounded-md bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800"
            >
              Next
              <Icon icon="mdi:arrow-right" className="h-4 w-4" />
            </button>
          ) : (
            <button
              type="button"
              onClick={() => void submit()}
              disabled={submitting}
              className="inline-flex items-center gap-2 rounded-md bg-slate-950 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800"
            >
              <Icon icon={submitting ? 'mdi:loading' : 'mdi:send'} className={`h-4 w-4 ${submitting ? 'animate-spin' : ''}`} />
              {submitting ? 'Submitting...' : 'Submit'}
            </button>
          )}
        </div>
      </article>

      {showSelfReportModal ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 px-4">
          <div className="w-full max-w-sm rounded-lg bg-white p-6 text-center shadow-xl">
            <h3 className="text-lg font-semibold text-slate-900">Ai trișat la acest examen?</h3>
            <p className="mt-2 text-sm text-slate-600">
              Te rugăm să răspunzi sincer — răspunsul nu influențează nota. Această platformă este realizată exclusiv pentru colectarea
              de date reale, necesare dezvoltării ulterioare a tezei de master.
            </p>
            <div className="mt-5 flex justify-center gap-3">
              <button
                type="button"
                onClick={() => void answerSelfReport(true)}
                disabled={selfReportSubmitting}
                className="inline-flex items-center gap-2 rounded-md bg-rose-700 px-4 py-2 text-sm font-semibold text-white hover:bg-rose-800 disabled:opacity-60"
              >
                Da
              </button>
              <button
                type="button"
                onClick={() => void answerSelfReport(false)}
                disabled={selfReportSubmitting}
                className="inline-flex items-center gap-2 rounded-md bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800 disabled:opacity-60"
              >
                Nu
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {showResult ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 px-4">
          <div className="w-full max-w-sm rounded-lg bg-white p-6 text-center shadow-xl">
            <Icon icon="mdi:check-circle" className="mx-auto h-10 w-10 text-emerald-600" />
            <h3 className="mt-2 text-lg font-semibold text-slate-900">Examen finalizat</h3>
            {finalScore !== null ? (
              <p className="mt-3 text-3xl font-bold tabular-nums text-slate-950">
                {finalScore} <span className="text-lg font-medium text-slate-500">/ {totalPoints}</span>
              </p>
            ) : (
              <p className="mt-3 text-sm text-slate-600">
                Răspunsurile tale au fost înregistrate. Nota finală va fi disponibilă după ce profesorul corectează manual întrebările deschise.
              </p>
            )}
            <button
              type="button"
              onClick={onFinished}
              className="mt-5 inline-flex w-full items-center justify-center gap-2 rounded-md bg-slate-950 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-800"
            >
              Închide
            </button>
          </div>
        </div>
      ) : null}
    </section>
  )
}

const freeTextMaxLength = 19000

function QuestionAnswer({ question, value, onChange }: { question: Question; value: AnswerValue | undefined; onChange: (value: AnswerValue) => void }) {
  if (question.type === 'free_text') {
    const text = typeof value === 'string' ? value : ''
    return (
      <div>
        <textarea
          className="min-h-40 w-full rounded-md border border-slate-300 px-3 py-2 outline-none focus:border-emerald-600"
          value={text}
          onChange={(event) => onChange(event.target.value)}
          maxLength={freeTextMaxLength}
        />
        <p className="mt-1 text-right text-xs text-slate-400">
          {text.length} / {freeTextMaxLength}
        </p>
      </div>
    )
  }

  const selected = Array.isArray(value) ? value : []
  return (
    <div className="space-y-2">
      {question.options.map((option) => {
        const checked = selected.includes(option.id)
        return (
          <label key={option.id} className="flex cursor-pointer items-start gap-3 rounded-md border border-slate-300 px-3 py-3 hover:bg-slate-50">
            <input
              className="mt-1"
              type={question.type === 'multiple_choice' ? 'checkbox' : 'radio'}
              name={question.id}
              checked={checked}
              onChange={() => {
                if (question.type === 'multiple_choice') {
                  onChange(checked ? selected.filter((id) => id !== option.id) : [...selected, option.id])
                } else {
                  onChange([option.id])
                }
              }}
            />
            <span>{option.label}</span>
          </label>
        )
      })}
    </div>
  )
}

function isAnswered(question: Question, value: AnswerValue | undefined) {
  if (question.type === 'free_text') {
    return typeof value === 'string' && value.trim().length > 0
  }

  return Array.isArray(value) && value.length > 0
}

function getAnswerLength(value: AnswerValue | undefined) {
  if (typeof value === 'string') {
    return value.length
  }

  return Array.isArray(value) ? value.length : 0
}

function toAnswerPayload(question: Question, value: AnswerValue | undefined) {
  if (question.type === 'free_text') {
    return { text: typeof value === 'string' ? value : '' }
  }

  return { selectedOptionIds: Array.isArray(value) ? value : [] }
}

function hydrateAnswers(questions: Question[], existingAnswers: ExistingAnswer[]): Record<string, AnswerValue> {
  const questionsById = new Map(questions.map((question) => [question.id, question]))
  const hydrated: Record<string, AnswerValue> = {}

  for (const existing of existingAnswers) {
    const question = questionsById.get(existing.questionId)
    if (!question) {
      continue
    }

    const value = fromAnswerPayload(question, existing.answerJson)
    if (value !== undefined) {
      hydrated[existing.questionId] = value
    }
  }

  return hydrated
}

function fromAnswerPayload(question: Question, answerJson: string): AnswerValue | undefined {
  try {
    const payload: unknown = JSON.parse(answerJson)
    if (!payload || typeof payload !== 'object') {
      return undefined
    }

    if (question.type === 'free_text') {
      const text = (payload as { text?: unknown }).text
      return typeof text === 'string' ? text : undefined
    }

    const selectedOptionIds = (payload as { selectedOptionIds?: unknown }).selectedOptionIds
    return Array.isArray(selectedOptionIds) ? selectedOptionIds.filter((id): id is string => typeof id === 'string') : undefined
  } catch {
    return undefined
  }
}

function formatRemaining(ms: number) {
  const totalSeconds = Math.ceil(ms / 1000)
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60

  return hours > 0
    ? `${hours}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
    : `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
}

function CountdownClock({ remainingMs }: { remainingMs: number }) {
  const totalSeconds = Math.ceil(remainingMs / 1000)
  const isCritical = totalSeconds <= 60
  const isWarning = !isCritical && totalSeconds <= 300

  const toneClasses = isCritical
    ? 'border-rose-600 bg-rose-950 text-rose-300'
    : isWarning
      ? 'border-amber-500 bg-amber-950 text-amber-300'
      : 'border-emerald-700 bg-slate-950 text-emerald-400'

  return (
    <div className={`mb-4 rounded-lg border-2 p-3 text-center ${toneClasses} ${isCritical ? 'animate-pulse' : ''}`}>
      <p className="text-[10px] font-semibold uppercase tracking-widest opacity-70">Time left</p>
      <p className="font-mono text-4xl font-bold tabular-nums tracking-widest">{formatRemaining(remainingMs)}</p>
    </div>
  )
}
