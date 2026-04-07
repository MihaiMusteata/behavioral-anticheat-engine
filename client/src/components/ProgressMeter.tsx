type ProgressMeterProps = {
  current: number
  total: number
}

export function ProgressMeter({ current, total }: ProgressMeterProps) {
  const progress = Math.round((current / total) * 100)

  return (
    <div aria-label={`Question ${current} of ${total}`} className="space-y-3">
      <div className="flex items-center justify-between gap-4 text-sm">
        <span className="font-medium text-slate-700 dark:text-slate-300">Question progress</span>
        <span className="font-semibold text-slate-950 dark:text-white">
          {current} / {total}
        </span>
      </div>
      <div
        role="progressbar"
        aria-valuenow={current}
        aria-valuemin={1}
        aria-valuemax={total}
        className="h-2 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-800"
      >
        <div
          className="h-full rounded-full bg-emerald-700 transition-[width] duration-300 dark:bg-emerald-400"
          style={{ width: `${progress}%` }}
        />
      </div>
    </div>
  )
}
