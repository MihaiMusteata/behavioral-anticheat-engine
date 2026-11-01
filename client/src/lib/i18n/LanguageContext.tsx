import { useMemo, useState, type ReactNode } from 'react'
import { en, ro, type Language, type TranslationDictionary } from './translations'
import { LanguageContext, type LanguageContextValue } from './language-context'

const storageKey = 'bae.language'
const dictionaries: Record<Language, TranslationDictionary> = { en, ro }

function readStoredLanguage(): Language {
  try {
    const stored = localStorage.getItem(storageKey)
    if (stored === 'en' || stored === 'ro') {
      return stored
    }
  } catch {
    // localStorage unavailable (private browsing, etc.) - fall through to the hardcoded default.
  }

  // Hardcoded default, no browser-language detection - a manual switch (the
  // language switcher) still overrides this and persists via storageKey.
  return 'ro'
}

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<Language>(readStoredLanguage)

  const value = useMemo<LanguageContextValue>(() => ({
    language,
    setLanguage: (next) => {
      setLanguageState(next)
      try {
        localStorage.setItem(storageKey, next)
      } catch {
        // Best-effort persistence only.
      }
    },
    t: dictionaries[language],
  }), [language])

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>
}
