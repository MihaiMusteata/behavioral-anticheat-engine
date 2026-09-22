const activeExamScheduleIdKey = 'bae.activeExamScheduleId'
const activeQuestionIndexKey = 'bae.activeQuestionIndex'

type StoredQuestionIndex = {
  sessionId: string
  index: number
}

export function saveActiveExamScheduleId(examScheduleId: string) {
  localStorage.setItem(activeExamScheduleIdKey, examScheduleId)
}

export function readActiveExamScheduleId(): string | null {
  return localStorage.getItem(activeExamScheduleIdKey)
}

export function clearActiveExamScheduleId() {
  localStorage.removeItem(activeExamScheduleIdKey)
}

export function saveActiveQuestionIndex(sessionId: string, index: number) {
  localStorage.setItem(activeQuestionIndexKey, JSON.stringify({ sessionId, index }))
}

export function readActiveQuestionIndex(sessionId: string): number | null {
  const raw = localStorage.getItem(activeQuestionIndexKey)
  if (!raw) {
    return null
  }

  try {
    const parsed = JSON.parse(raw) as StoredQuestionIndex
    return parsed.sessionId === sessionId && Number.isInteger(parsed.index) ? parsed.index : null
  } catch {
    return null
  }
}

export function clearActiveQuestionIndex() {
  localStorage.removeItem(activeQuestionIndexKey)
}
