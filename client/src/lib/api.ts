import axios, { AxiosError, type AxiosRequestConfig } from 'axios'

const accessTokenKey = 'bae.accessToken'
const refreshTokenKey = 'bae.refreshToken'
const userKey = 'bae.user'
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

export type Role = 'Student' | 'Proctor' | 'Admin'

export type AuthUser = {
  userId: string
  email: string
  role: Role
}

export type AuthResponse = AuthUser & {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
}

export const api = axios.create({
  baseURL: apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(accessTokenKey)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

let refreshPromise: Promise<AuthResponse | null> | null = null

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retry?: boolean }) | undefined
    if (error.response?.status !== 401 || !original || original._retry) {
      throw error
    }

    const refreshToken = localStorage.getItem(refreshTokenKey)
    if (!refreshToken) {
      clearStoredAuth()
      throw error
    }

    original._retry = true
    refreshPromise ??= api
      .post<AuthResponse>('/api/auth/refresh', { refreshToken })
      .then((response) => {
        storeAuth(response.data)
        return response.data
      })
      .catch(() => {
        clearStoredAuth()
        return null
      })
      .finally(() => {
        refreshPromise = null
      })

    const refreshed = await refreshPromise
    if (!refreshed) {
      throw error
    }

    original.headers = {
      ...original.headers,
      Authorization: `Bearer ${refreshed.accessToken}`,
    }

    return api(original)
  },
)

export function storeAuth(auth: AuthResponse) {
  const user: AuthUser = {
    userId: auth.userId,
    email: auth.email,
    role: auth.role,
  }

  localStorage.setItem(accessTokenKey, auth.accessToken)
  localStorage.setItem(refreshTokenKey, auth.refreshToken)
  localStorage.setItem(userKey, JSON.stringify(user))
}

export function clearStoredAuth() {
  localStorage.removeItem(accessTokenKey)
  localStorage.removeItem(refreshTokenKey)
  localStorage.removeItem(userKey)
}

export function readStoredUser(): AuthUser | null {
  const raw = localStorage.getItem(userKey)
  if (!raw) {
    return null
  }

  try {
    return JSON.parse(raw) as AuthUser
  } catch {
    clearStoredAuth()
    return null
  }
}

export function getAccessToken() {
  return localStorage.getItem(accessTokenKey)
}

export function resolveApiUrl(path: string) {
  if (!apiBaseUrl) {
    return path
  }

  return new URL(path, apiBaseUrl).toString()
}

export function getErrorMessage(error: unknown) {
  if (axios.isAxiosError<{ error?: string }>(error)) {
    return error.response?.data?.error ?? error.message
  }

  return error instanceof Error ? error.message : 'Unexpected error'
}
