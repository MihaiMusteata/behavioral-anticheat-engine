import { useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Icon } from '@iconify/react'
import { useLanguage } from '../../lib/i18n/language-context'
import type { DetectionVote, QuestionDetection, SessionDetection } from './types'

// Suspicion from the AI voting ensemble (ml-service via the Worker).
// waiting -> not submitted yet, processing -> queued / being scored,
// completed -> score + level, with every voter's result in an info tooltip.

const LEVEL_STYLE: Record<string, { bar: string; text: string }> = {
  high: { bar: 'bg-rose-600', text: 'text-rose-700' },
  medium: { bar: 'bg-amber-500', text: 'text-amber-700' },
  low: { bar: 'bg-emerald-600', text: 'text-emerald-700' },
}
const UNKNOWN_STYLE = { bar: 'bg-slate-400', text: 'text-slate-600' }

export function SuspicionCell({ detection }: { detection: SessionDetection }) {
  const { t } = useLanguage()

  if (detection.status === 'completed') {
    return (
      <ScoreBar score={detection.score} level={detection.level}>
        <VotesTooltip
          title={t.admin.detection.tooltipTitle}
          ensembleLabel={t.admin.detection.ensembleLabel}
          score={detection.score}
          level={detection.level}
          votersUsed={detection.votersUsed}
          models={detection.models}
        />
      </ScoreBar>
    )
  }

  return <DetectionStatus status={detection.status} />
}

/** Session detail header: the session result, or the selected question's. */
export function DetectionPanel({ session, question }: { session: SessionDetection; question?: QuestionDetection | null }) {
  const { t } = useLanguage()
  const scoped = question !== undefined

  return (
    <div className="space-y-1">
      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
        {scoped ? t.admin.detection.questionScope : t.admin.detection.sessionScope}
      </p>
      {session.status !== 'completed' ? (
        <DetectionStatus status={session.status} />
      ) : scoped && !question ? (
        <p className="text-xs text-slate-500">{t.admin.detection.noScore}</p>
      ) : scoped && question ? (
        <ScoreBar score={question.score} level={question.level}>
          <VotesTooltip
            title={t.admin.detection.tooltipTitle}
            ensembleLabel={t.admin.detection.questionEnsembleLabel}
            score={question.score}
            level={question.level}
            votersUsed={question.votersUsed}
            suspectVotes={question.suspectVotes}
            models={question.models}
          />
        </ScoreBar>
      ) : (
        <SuspicionCell detection={session} />
      )}
    </div>
  )
}

/** Compact suspicion of one question, for lists (no scope header). */
export function QuestionSuspicion({ session, question, emptyLabel }: {
  session: SessionDetection
  question: QuestionDetection | null
  emptyLabel: string
}) {
  const { t } = useLanguage()

  if (session.status !== 'completed') {
    return <DetectionStatus status={session.status} />
  }
  if (!question) {
    return <span className="text-xs text-slate-400">{emptyLabel}</span>
  }

  return (
    <div>
      <ScoreBar score={question.score} level={question.level}>
      <VotesTooltip
        title={t.admin.detection.tooltipTitle}
        ensembleLabel={t.admin.detection.questionEnsembleLabel}
        score={question.score}
        level={question.level}
        votersUsed={question.votersUsed}
        suspectVotes={question.suspectVotes}
        models={question.models}
      />
      </ScoreBar>
      {question.suspectVotes !== null && question.votersUsed !== null ? (
        <p className="mt-1 text-[11px] text-slate-500">{t.admin.detection.votesShort(question.suspectVotes, question.votersUsed)}</p>
      ) : null}
    </div>
  )
}

function DetectionStatus({ status }: { status: SessionDetection['status'] }) {
  const { t } = useLanguage()
  const view = {
    waiting: { icon: 'mdi:clock-outline', label: t.admin.detection.waiting, hint: t.admin.detection.waitingHint, tone: 'bg-slate-100 text-slate-700', spin: false },
    processing: { icon: 'mdi:loading', label: t.admin.detection.processing, hint: t.admin.detection.processingHint, tone: 'bg-sky-50 text-sky-800', spin: true },
    failed: { icon: 'mdi:alert-circle-outline', label: t.admin.detection.failed, hint: t.admin.detection.failedHint, tone: 'bg-rose-50 text-rose-700', spin: false },
    completed: { icon: 'mdi:check', label: '', hint: '', tone: '', spin: false },
  }[status]

  return (
    <span title={view.hint} className={`inline-flex items-center gap-1 rounded px-2 py-1 text-xs font-semibold ${view.tone}`}>
      <Icon icon={view.icon} className={`h-4 w-4 ${view.spin ? 'animate-spin' : ''}`} />
      {view.label}
    </span>
  )
}

function ScoreBar({ score, level, children }: { score: number | null; level: string | null; children: ReactNode }) {
  const { t } = useLanguage()
  const style = (level && LEVEL_STYLE[level]) || UNKNOWN_STYLE
  const percent = score === null ? null : Math.round(Math.max(0, Math.min(score, 1)) * 100)

  return (
    <div className="min-w-44">
      <div className="mb-1 flex items-center justify-between gap-2 text-xs font-semibold text-slate-700">
        <span className={style.text}>
          {percent === null ? t.admin.detection.noScore : `${percent}%`}
          {level ? <span className="ml-1 font-normal text-slate-500">· {t.admin.detection.levels[level] ?? level}</span> : null}
        </span>
        <InfoTooltip label={t.admin.detection.infoButton}>{children}</InfoTooltip>
      </div>
      <div className="h-2 rounded bg-slate-200">
        <div className={`h-2 rounded ${style.bar}`} style={{ width: `${percent ?? 0}%` }} />
      </div>
    </div>
  )
}

function VotesTooltip({ title, ensembleLabel, score, level, votersUsed, models, suspectVotes = null }: {
  title: string
  ensembleLabel: string
  score: number | null
  level: string | null
  votersUsed: number | null
  models: DetectionVote[]
  suspectVotes?: number | null
}) {
  const { t } = useLanguage()

  return (
    <div className="space-y-2">
      <p className="font-semibold text-slate-900">{title}</p>
      <ul className="space-y-1">
        {models.map((model) => (
          <li key={model.model} className="flex items-center justify-between gap-3">
            <span className="text-slate-700">{t.admin.detection.models[model.model] ?? model.model}</span>
            <span className="flex items-center gap-2 tabular-nums">
              <span className="font-semibold text-slate-900">
                {model.probability === null ? '—' : `${Math.round(model.probability * 100)}%`}
              </span>
              <span className={`rounded px-1.5 py-0.5 text-[10px] font-semibold ${voteTone(model.vote)}`}>
                {t.admin.detection.votes[model.vote] ?? model.vote}
              </span>
            </span>
          </li>
        ))}
      </ul>
      <div className="flex items-center justify-between gap-3 border-t border-slate-200 pt-2">
        <span className="font-semibold text-slate-900">{ensembleLabel}</span>
        <span className="font-semibold tabular-nums text-slate-900">
          {score === null ? '—' : `${Math.round(score * 100)}%`}
          {level ? <span className="ml-1 font-normal text-slate-500">· {t.admin.detection.levels[level] ?? level}</span> : null}
        </span>
      </div>
      {suspectVotes !== null && votersUsed !== null ? (
        <p className="font-semibold text-slate-700">{t.admin.detection.majorityVotes(suspectVotes, votersUsed)}</p>
      ) : votersUsed !== null ? (
        <p className="text-slate-500">{t.admin.detection.votersUsed(votersUsed)}</p>
      ) : null}
      <p className="text-slate-400">{t.admin.detection.disclaimer}</p>
    </div>
  )
}

function voteTone(vote: string) {
  if (vote === 'suspect') {
    return 'bg-rose-50 text-rose-700'
  }
  if (vote === 'not_suspect') {
    return 'bg-emerald-50 text-emerald-700'
  }
  return 'bg-slate-100 text-slate-600'
}

const TOOLTIP_WIDTH = 288

/** Hover/focus tooltip rendered in a portal, so table overflow cannot clip it. */
function InfoTooltip({ label, children }: { label: string; children: ReactNode }) {
  const buttonRef = useRef<HTMLButtonElement>(null)
  const [position, setPosition] = useState<{ top: number; left: number; above: boolean } | null>(null)

  function show() {
    const rect = buttonRef.current?.getBoundingClientRect()
    if (!rect) {
      return
    }
    const above = rect.bottom + 280 > window.innerHeight
    setPosition({
      top: above ? rect.top - 6 : rect.bottom + 6,
      left: Math.max(8, Math.min(rect.right - TOOLTIP_WIDTH, window.innerWidth - TOOLTIP_WIDTH - 8)),
      above,
    })
  }

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        aria-label={label}
        onMouseEnter={show}
        onMouseLeave={() => setPosition(null)}
        onFocus={show}
        onBlur={() => setPosition(null)}
        className="rounded p-0.5 text-slate-500 hover:bg-slate-100 hover:text-slate-800 focus:outline-none focus:ring-2 focus:ring-emerald-600"
      >
        <Icon icon="mdi:information-outline" className="h-4 w-4" />
      </button>
      {position
        ? createPortal(
          <div
            role="tooltip"
            style={{ position: 'fixed', top: position.top, left: position.left, width: TOOLTIP_WIDTH, transform: position.above ? 'translateY(-100%)' : undefined }}
            className="z-50 rounded-md border border-slate-200 bg-white p-3 text-xs font-normal normal-case shadow-lg"
          >
            {children}
          </div>,
          document.body,
        )
        : null}
    </>
  )
}
