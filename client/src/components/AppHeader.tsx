import { Icon } from '@iconify/react'
import { ThemeToggle } from './ThemeToggle'

type AppHeaderProps = {
  isDarkMode: boolean
  onToggleTheme: () => void
}

export function AppHeader({ isDarkMode, onToggleTheme }: AppHeaderProps) {
  return (
    <header className="border-b border-slate-200 bg-white/90 dark:border-slate-800 dark:bg-slate-950/90">
      <div className="mx-auto flex w-full max-w-6xl items-center justify-between gap-4 px-4 py-4 sm:px-6 lg:px-8">
        <div className="flex min-w-0 items-center gap-3">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-slate-950 text-white dark:bg-white dark:text-slate-950">
            <Icon icon="lucide:graduation-cap" className="h-5 w-5" aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <p className="text-sm font-medium text-slate-500 dark:text-slate-400">University assessment</p>
            <h1 className="truncate text-lg font-semibold text-slate-950 dark:text-white sm:text-xl">
              Secure Web Concepts Quiz
            </h1>
          </div>
        </div>
        <ThemeToggle isDarkMode={isDarkMode} onToggle={onToggleTheme} />
      </div>
    </header>
  )
}
