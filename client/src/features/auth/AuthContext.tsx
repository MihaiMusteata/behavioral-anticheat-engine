import { useMemo, useState, type ReactNode } from 'react'
import { api, clearStoredAuth, readStoredUser, storeAuth, type AuthResponse, type AuthUser } from '../../lib/api'
import type { GuestJoinResponse } from '../exams/types'
import { AuthContext, type AuthContextValue } from './auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(() => readStoredUser())
  // Captured once at mount - never updated again - to distinguish "a user
  // was already persisted at page load" (a real cross-session restore) from
  // "the user just logged in/joined during this page session".
  const [restoredFromStorage] = useState(() => user !== null)

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      restoredFromStorage,
      async login(email, password) {
        const response = await api.post<AuthResponse>('/api/auth/login', { email, password })
        storeAuth(response.data)
        setUser({
          userId: response.data.userId,
          email: response.data.email,
          role: response.data.role,
        })
      },
      async signUp(email, password, role) {
        const response = await api.post<AuthResponse>('/api/auth/signup', { email, password, role })
        storeAuth(response.data)
        setUser({
          userId: response.data.userId,
          email: response.data.email,
          role: response.data.role,
        })
      },
      async joinExam(pinCode, participantName, consentAccepted) {
        const response = await api.post<GuestJoinResponse>('/api/guest/join', { pinCode, participantName, consentAccepted })
        const guestUser: AuthUser = { userId: response.data.userId, email: '', role: 'Student' }
        storeAuth({
          ...guestUser,
          accessToken: response.data.accessToken,
          accessTokenExpiresAtUtc: response.data.accessTokenExpiresAtUtc,
          refreshToken: response.data.refreshToken,
          refreshTokenExpiresAtUtc: response.data.refreshTokenExpiresAtUtc,
        })
        setUser(guestUser)
        return response.data.session
      },
      logout() {
        clearStoredAuth()
        setUser(null)
      },
    }),
    [user, restoredFromStorage],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
