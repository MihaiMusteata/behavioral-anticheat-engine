import { useLanguage } from './language-context'
import type { Language } from './translations'

const options: { value: Language; label: string }[] = [
  { value: 'ro', label: 'RO' },
  { value: 'en', label: 'EN' },
]

export function LanguageSwitcher() {
  const { language, setLanguage } = useLanguage()

  return (
    <div className="inline-flex rounded-md border border-slate-300 bg-white p-0.5 text-xs font-semibold">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          onClick={() => setLanguage(option.value)}
          aria-pressed={language === option.value}
          className={`rounded px-2 py-1 ${
            language === option.value ? 'bg-emerald-700 text-white' : 'text-slate-600 hover:bg-slate-100'
          }`}
        >
          {option.label}
        </button>
      ))}
    </div>
  )
}
