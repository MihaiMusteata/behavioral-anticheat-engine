import { Icon } from '@iconify/react'
import type { ReactNode } from 'react'
import { useAuth } from '../features/auth/auth-context'
import { useLanguage } from '../lib/i18n/language-context'
import { LanguageSwitcher } from '../lib/i18n/LanguageSwitcher'

export function Shell({ children, title }: { children: ReactNode; title: string }) {
  const { user, logout } = useAuth()
  const { t } = useLanguage()

  return (
    <div className="min-h-dvh bg-slate-100 text-slate-950">
      <header className="border-b border-slate-300 bg-white">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3">
          <div className="flex items-center gap-3">
            <div className="grid h-9 w-9 place-items-center rounded-md bg-emerald-700 text-white">
              <Icon icon="mdi:shield-check" className="h-5 w-5" />
            </div>
            <div>
              <h1 className="text-base font-semibold">{title}</h1>
              <p className="text-xs text-slate-500">{user?.email ? `${user.email} · ` : ''}{user?.role}</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <LanguageSwitcher />
            <button
              type="button"
              onClick={logout}
              className="inline-flex items-center gap-2 rounded-md border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              <Icon icon="mdi:logout" className="h-4 w-4" />
              {t.common.logout}
            </button>
          </div>
        </div>
      </header>
      {children}
    </div>
  )
}
