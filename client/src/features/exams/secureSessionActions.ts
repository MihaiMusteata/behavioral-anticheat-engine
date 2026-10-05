import { api } from '../../lib/api'
import { buildEnvelope, deriveSessionKeys } from '../../lib/secureEnvelope'

const actionSeqStorageKeyPrefix = 'bae.actionSeq.'

function nextActionSeq(sessionId: string): number {
  const key = actionSeqStorageKeyPrefix + sessionId
  const current = Number(sessionStorage.getItem(key) ?? '0')
  const next = (Number.isSafeInteger(current) && current >= 0 ? current : 0) + 1
  sessionStorage.setItem(key, String(next))
  return next
}

async function postSecure<TResponse>(path: string, sessionId: string, sessionSecret: string, payload: unknown) {
  const keys = await deriveSessionKeys(sessionSecret)
  const envelope = await buildEnvelope(sessionId, nextActionSeq(sessionId), payload, keys)
  const response = await api.post<TResponse>(path, envelope)
  return response.data
}

async function putSecure<TResponse>(path: string, sessionId: string, sessionSecret: string, payload: unknown) {
  const keys = await deriveSessionKeys(sessionSecret)
  const envelope = await buildEnvelope(sessionId, nextActionSeq(sessionId), payload, keys)
  const response = await api.put<TResponse>(path, envelope)
  return response.data
}

export function saveAnswerSecure<TResponse>(sessionId: string, sessionSecret: string, questionId: string, answer: unknown) {
  return putSecure<TResponse>(`/api/student/sessions/${sessionId}/answers`, sessionId, sessionSecret, { questionId, answer })
}

export function submitExamSecure<TResponse>(sessionId: string, sessionSecret: string) {
  return postSecure<TResponse>(`/api/student/sessions/${sessionId}/submit`, sessionId, sessionSecret, {})
}

export function selfReportSecure<TResponse>(sessionId: string, sessionSecret: string, cheated: boolean) {
  return putSecure<TResponse>(`/api/student/sessions/${sessionId}/self-report`, sessionId, sessionSecret, { cheated })
}
