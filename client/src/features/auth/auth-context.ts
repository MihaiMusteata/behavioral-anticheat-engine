import { createContext, useContext } from 'react'
import type { AuthUser, Role } from '../../lib/api'
import type { StartExamSessionResponse } from '../exams/types'

export type AuthContextValue = {
  user: AuthUser | null
  /** True when `user` was already persisted in localStorage at page load (a genuine cross-session restore), false if they just logged in/joined during this page session. */
  restoredFromStorage: boolean
  login: (email: string, password: string) => Promise<void>
  signUp: (email: string, password: string, role: Exclude<Role, 'Admin'>) => Promise<void>
  joinExam: (pinCode: string, participantName: string, consentAccepted: boolean) => Promise<StartExamSessionResponse>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used inside AuthProvider')
  }

  return value
}
