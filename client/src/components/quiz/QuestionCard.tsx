import { Icon } from '@iconify/react'
import type { AnswerValue, Question } from '../../types/quiz'
import { questionTypeLabels } from '../../utils/quiz'
import { ProgressMeter } from '../ProgressMeter'
import { QuestionInput } from './QuestionInput'

type QuestionCardProps = {
  question: Question
  answer: AnswerValue | undefined
  currentQuestionNumber: number
  totalQuestions: number
  isLastQuestion: boolean
  onAnswerChange: (value: AnswerValue) => void
  onNext: () => void
}

export function QuestionCard({
  question,
  answer,
  currentQuestionNumber,
  totalQuestions,
  isLastQuestion,
  onAnswerChange,
  onNext,
}: QuestionCardProps) {
  return (
    <section
      aria-labelledby="question-title"
      className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900/70 sm:p-6 lg:p-8"
    >
      <div className="mb-8">
        <ProgressMeter current={currentQuestionNumber} total={totalQuestions} />
      </div>

      <div className="flex flex-col gap-4 border-b border-slate-200 pb-6 dark:border-slate-800 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <span className="rounded-md bg-slate-100 px-2.5 py-1 text-sm font-medium text-slate-700 dark:bg-slate-800 dark:text-slate-300">
              {question.topic}
            </span>
            <span className="rounded-md border border-emerald-200 px-2.5 py-1 text-sm font-medium text-emerald-700 dark:border-emerald-900 dark:text-emerald-300">
              {questionTypeLabels[question.type]}
            </span>
          </div>
          <p className="text-sm font-semibold uppercase text-emerald-700 dark:text-emerald-300">
            Question {currentQuestionNumber}
          </p>
          <h2
            id="question-title"
            className="mt-3 text-2xl font-semibold leading-tight text-slate-950 dark:text-white sm:text-3xl"
          >
            {question.prompt}
          </h2>
        </div>
        <div className="flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-600 dark:border-slate-800 dark:bg-slate-950 dark:text-slate-400">
          <Icon icon="lucide:file-text" className="h-4 w-4" aria-hidden="true" />
          <span>{totalQuestions - currentQuestionNumber} remaining</span>
        </div>
      </div>

      <div className="py-7">
        <QuestionInput question={question} answer={answer} onChange={onAnswerChange} />
      </div>

      <div className="flex flex-col-reverse gap-3 border-t border-slate-200 pt-6 dark:border-slate-800 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm leading-6 text-slate-500 dark:text-slate-400">
          Navigation is forward only for this exam session.
        </p>
        <button
          type="button"
          onClick={onNext}
          className="inline-flex w-full items-center justify-center gap-2 rounded-md bg-emerald-700 px-5 py-3 text-base font-semibold text-white shadow-sm transition hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 dark:bg-emerald-500 dark:text-slate-950 dark:hover:bg-emerald-400 dark:focus:ring-emerald-300 dark:focus:ring-offset-slate-900 sm:w-auto"
        >
          {isLastQuestion ? 'Finish quiz' : 'Next question'}
          <Icon icon="lucide:arrow-right" className="h-5 w-5" aria-hidden="true" />
        </button>
      </div>
    </section>
  )
}
