import { createContext, useContext } from 'react'
import type { Language, TranslationDictionary } from './translations'

export type LanguageContextValue = {
  language: Language
  setLanguage: (language: Language) => void
  t: TranslationDictionary
}

export const LanguageContext = createContext<LanguageContextValue | null>(null)

export function useLanguage() {
  const value = useContext(LanguageContext)
  if (!value) {
    throw new Error('useLanguage must be used inside LanguageProvider')
  }

  return value
}
