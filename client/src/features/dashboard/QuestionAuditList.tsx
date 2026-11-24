import { Icon } from '@iconify/react'
import { useLanguage } from '../../lib/i18n/language-context'
import { markdownToPlainText } from '../../lib/markdownPlainText'
import { QuestionSuspicion } from './DetectionBadge'
import { suspicionHighlight } from './suspicionHighlight'
import type { SessionDetection, SessionTimeline } from './types'

export type QuestionAuditItem = {
  question: SessionTimeline['questions'][number]
  eventCount: number
  answered: boolean
}

const PROMPT_PREVIEW_LENGTH = 140

/**
 * "Entire audit, grouped by question": one row per question instead of the
 * event list. Clicking a row opens that question's audit. Each row shows the
 * question's AI status / majority-vote result (3 voters) and is tinted by
 * its suspicion score.
 */
export function QuestionAuditList({ items, sessionDetection, onOpenQuestion }: {
  items: QuestionAuditItem[]
  sessionDetection: SessionDetection
  onOpenQuestion: (questionId: string) => void
}) {
  const { t } = useLanguage()
  const texts = t.admin.sessionDetail

  return (
    <div>
      <div className="mb-3">
        <h3 className="font-semibold">{texts.questionsHeading}</h3>
        <p className="text-xs text-slate-500">{texts.questionsHint}</p>
      </div>

      <ul className="max-h-[520px] space-y-2 overflow-auto">
        {items.map(({ question, eventCount, answered }) => (
          <li
            key={question.questionId}
            className="rounded-md border border-slate-200 transition-shadow hover:shadow-md"
            style={sessionDetection.status === 'completed' ? suspicionHighlight(question.detection?.score) : undefined}
          >
            <div className="flex flex-wrap items-center gap-3 p-3">
              <button
                type="button"
                onClick={() => onOpenQuestion(question.questionId)}
                aria-label={`${texts.openQuestionAudit}: ${question.label}`}
                className="min-w-0 flex-1 text-left focus:outline-none focus:ring-2 focus:ring-emerald-600"
              >
                <span className="flex items-center gap-2 text-xs font-semibold uppercase text-slate-500">
                  {question.label}
                  <Icon icon="mdi:chevron-right" className="h-4 w-4" />
                </span>
                <span className="mt-1 block truncate text-sm text-slate-800">
                  {previewPrompt(question.prompt)}
                </span>
                <span className="mt-1 flex flex-wrap gap-2 text-xs text-slate-500">
                  <span className="inline-flex items-center gap-1">
                    <Icon icon="mdi:timeline-text-outline" className="h-3.5 w-3.5" />
                    {texts.questionEventsCount(eventCount)}
                  </span>
                  <span className={`inline-flex items-center gap-1 ${answered ? 'text-emerald-700' : 'text-slate-400'}`}>
                    <Icon icon={answered ? 'mdi:check-circle-outline' : 'mdi:circle-outline'} className="h-3.5 w-3.5" />
                    {answered ? texts.answered : texts.notAnswered}
                  </span>
                </span>
              </button>

              <div className="w-56 shrink-0">
                <p className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-slate-400">{texts.questionSuspicionLabel}</p>
                <QuestionSuspicion session={sessionDetection} question={question.detection} emptyLabel={texts.noQuestionDetection} />
              </div>
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}

function previewPrompt(prompt: string) {
  const plain = markdownToPlainText(prompt).replace(/\s+/g, ' ').trim()
  return plain.length > PROMPT_PREVIEW_LENGTH ? `${plain.slice(0, PROMPT_PREVIEW_LENGTH)}…` : plain
}
