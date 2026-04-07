import { Icon } from '@iconify/react'

type CompletionPanelProps = {
  submitted: boolean
  onSubmit: () => void
}

export function CompletionPanel({ submitted, onSubmit }: CompletionPanelProps) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900/70 sm:p-8">
      <div className="mx-auto flex max-w-2xl flex-col items-center text-center">
        <div className="mb-5 flex h-12 w-12 items-center justify-center rounded-lg bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
          <Icon icon="lucide:check" className="h-6 w-6" aria-hidden="true" />
        </div>
        <p className="text-sm font-semibold uppercase text-emerald-700 dark:text-emerald-300">
          Assessment complete
        </p>
        <h2 className="mt-3 text-2xl font-semibold text-slate-950 dark:text-white sm:text-3xl">
          All 10 responses are ready for submission.
        </h2>
        <p className="mt-4 max-w-xl text-base leading-7 text-slate-600 dark:text-slate-400">
          Submit your quiz when you are ready. Responses cannot be edited after this point in the prototype flow.
        </p>
        <button
          type="button"
          onClick={onSubmit}
          disabled={submitted}
          className="mt-8 inline-flex w-full items-center justify-center gap-2 rounded-md bg-emerald-700 px-5 py-3 text-base font-semibold text-white shadow-sm transition hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:bg-slate-400 dark:bg-emerald-500 dark:text-slate-950 dark:hover:bg-emerald-400 dark:focus:ring-emerald-300 dark:focus:ring-offset-slate-950 dark:disabled:bg-slate-700 dark:disabled:text-slate-400 sm:w-auto"
        >
          <Icon icon="lucide:send" className="h-5 w-5" aria-hidden="true" />
          {submitted ? 'Submitted' : 'Submit assessment'}
        </button>
      </div>
    </section>
  )
}
