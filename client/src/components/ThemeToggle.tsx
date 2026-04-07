import { Icon } from '@iconify/react'

type ThemeToggleProps = {
  isDarkMode: boolean
  onToggle: () => void
}

export function ThemeToggle({ isDarkMode, onToggle }: ThemeToggleProps) {
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={isDarkMode}
      aria-label={isDarkMode ? 'Switch to light mode' : 'Switch to dark mode'}
      className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-md border border-slate-200 bg-white text-slate-700 shadow-sm transition hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800 dark:focus:ring-emerald-300 dark:focus:ring-offset-slate-950"
    >
      <Icon icon={isDarkMode ? 'lucide:sun' : 'lucide:moon'} className="h-5 w-5" aria-hidden="true" />
    </button>
  )
}
