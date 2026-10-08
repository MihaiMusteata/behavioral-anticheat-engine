import axios from 'axios'
import { api, getAccessToken, resolveApiUrl } from '../../lib/api'
import type { BehaviorContext, BehaviorEventDraft, BehaviorEventEnvelope, BehaviorPayload } from './types'

type QueuedBehaviorEvent = BehaviorEventDraft & {
  seq: number
  timestampMs: number
  nonce: string
}

type BehaviorCryptoKeys = {
  signingKey: CryptoKey
  encryptionKey: CryptoKey
}

type BehavioralEventResult = {
  seq: number
  accepted: boolean
  reason: string | null
  flags: string | null
}

type BehavioralEventBatchResult = {
  acceptedCount: number
  rejectedCount: number
  results: BehavioralEventResult[]
}

const maxBatchSize = 10
const flushIntervalMs = 5000

export class BehaviorEventCollector {
  private buffer: QueuedBehaviorEvent[] = []
  private flushTimer: number | null = null
  private seq: number
  private readonly seqStorageKey: string
  private readonly sessionId: string
  private readonly keys: Promise<BehaviorCryptoKeys>
  private context: BehaviorContext = {}
  private flushPromise: Promise<void> | null = null
  private preferKeepalive = false
  private stopped = false
  private sessionEndRecorded = false

  constructor(sessionId: string, sessionSecret: string) {
    this.sessionId = sessionId
    this.keys = deriveBehaviorKeys(sessionSecret)
    this.seqStorageKey = `bae.seq.${sessionId}`
    const storedSeq = Number(sessionStorage.getItem(this.seqStorageKey) ?? '0')
    this.seq = Number.isSafeInteger(storedSeq) && storedSeq >= 0 ? storedSeq : 0
  }

  start() {
    if (this.flushTimer !== null) {
      return
    }

    this.stopped = false
    this.flushTimer = window.setInterval(() => {
      void this.flush().catch(reportCollectorError)
    }, flushIntervalMs)
  }

  stop(keepalive = false) {
    if (this.flushTimer !== null) {
      window.clearInterval(this.flushTimer)
      this.flushTimer = null
    }

    this.stopped = true
    return keepalive ? this.flushKeepalive() : this.flush()
  }

  setContext(context: BehaviorContext) {
    this.context = context
  }

  record(eventType: string, data: BehaviorPayload = {}) {
    if (this.stopped) {
      return
    }

    if (eventType === 'session_end') {
      if (this.sessionEndRecorded) {
        return
      }

      this.sessionEndRecorded = true
    } else if (eventType === 'session_resume') {
      this.sessionEndRecorded = false
    }

    this.seq += 1
    sessionStorage.setItem(this.seqStorageKey, String(this.seq))
    this.buffer.push({
      eventType,
      data: { ...this.context, ...data },
      seq: this.seq,
      timestampMs: Date.now(),
      nonce: generateNonce(),
    })

    if (this.buffer.length >= maxBatchSize) {
      void this.flush().catch(reportCollectorError)
    }
  }

  flush(): Promise<void> {
    if (this.flushPromise) {
      return this.flushPromise
    }

    if (this.buffer.length === 0) {
      return Promise.resolve()
    }

    this.flushPromise = this.drainBuffer().finally(() => {
      this.flushPromise = null
    })

    return this.flushPromise
  }

  flushKeepalive(): Promise<void> {
    this.preferKeepalive = true
    return this.flush()
  }

  private async drainBuffer() {
    while (this.buffer.length > 0) {
      const batch = this.buffer.splice(0, maxBatchSize)
      let events: BehaviorEventEnvelope[]

      try {
        events = await Promise.all(batch.map((event) => this.toEnvelope(event)))
      } catch (error) {
        this.buffer.unshift(...batch)
        throw error
      }

      let result: BehavioralEventBatchResult
      try {
        const useKeepalive = this.preferKeepalive
        this.preferKeepalive = false
        result = useKeepalive
          ? await postWithKeepalive(events)
          : await postWithRetry(events)
      } catch (error) {
        this.buffer.unshift(...batch)
        throw error
      }

      const resultsBySeq = new Map(result.results.map((item) => [item.seq, item]))
      const retryable: QueuedBehaviorEvent[] = []
      const rejected: BehavioralEventResult[] = []

      batch.forEach((queuedEvent) => {
        const eventResult = resultsBySeq.get(queuedEvent.seq)
        if (!eventResult) {
          retryable.push(queuedEvent)
          return
        }

        if (!eventResult.accepted && isRetryableRejection(eventResult.reason)) {
          retryable.push(queuedEvent)
        } else if (!eventResult.accepted) {
          rejected.push(eventResult)
        }
      })

      if (rejected.length > 0) {
        console.warn('Behavioral events were rejected by the server.', rejected)
      }

      if (retryable.length > 0) {
        this.buffer.unshift(...retryable)
        throw new Error('Behavioral event batch was not fully acknowledged; retry is pending.')
      }
    }
  }

  private async toEnvelope(event: QueuedBehaviorEvent): Promise<BehaviorEventEnvelope> {
    const timestamp = new Date(event.timestampMs).toISOString()
    const eventData = {
      ...event.data,
      eventType: event.eventType,
    }
    const keys = await this.keys
    const nonceBytes = base64UrlToBytes(event.nonce)
    const data = await encryptEventData(eventData, keys.encryptionKey, nonceBytes)
    const signature = await signEnvelope(this.sessionId, event.seq, event.timestampMs, event.nonce, data, keys.signingKey)

    return {
      sessionId: this.sessionId,
      seq: event.seq,
      timestamp,
      nonce: event.nonce,
      data,
      signature,
    }
  }
}

async function postWithRetry(events: BehaviorEventEnvelope[]): Promise<BehavioralEventBatchResult> {
  let delayMs = 300
  let lastError: unknown

  for (let attempt = 0; attempt < 3; attempt += 1) {
    try {
      const response = await api.post<BehavioralEventBatchResult>('/api/behavior/events', { events })
      return normalizeBatchResult(response.data)
    } catch (error) {
      if (axios.isAxiosError<BehavioralEventBatchResult>(error) && error.response?.status === 400 && error.response.data) {
        return normalizeBatchResult(error.response.data)
      }

      lastError = error
      await new Promise((resolve) => window.setTimeout(resolve, delayMs))
      delayMs *= 2
    }
  }

  throw lastError
}

async function postWithKeepalive(events: BehaviorEventEnvelope[]): Promise<BehavioralEventBatchResult> {
  const token = getAccessToken()
  const response = await fetch(resolveApiUrl('/api/behavior/events'), {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify({ events }),
    keepalive: true,
  })

  if (response.status === 400) {
    return normalizeBatchResult(await response.json() as BehavioralEventBatchResult)
  }

  if (!response.ok) {
    throw new Error(`Behavioral keepalive request failed with HTTP ${response.status}.`)
  }

  return normalizeBatchResult(await response.json() as BehavioralEventBatchResult)
}

function normalizeBatchResult(result: BehavioralEventBatchResult): BehavioralEventBatchResult {
  type RawBehavioralEventResult = Partial<BehavioralEventResult> & {
    Seq?: number
    Accepted?: boolean
    Reason?: string | null
    Flags?: string | null
  }
  const candidate = result as unknown as {
    acceptedCount?: number
    rejectedCount?: number
    results?: RawBehavioralEventResult[]
    AcceptedCount?: number
    RejectedCount?: number
    Results?: RawBehavioralEventResult[]
  }
  const rawResults = candidate.results ?? candidate.Results ?? []

  return {
    acceptedCount: candidate.acceptedCount ?? candidate.AcceptedCount ?? 0,
    rejectedCount: candidate.rejectedCount ?? candidate.RejectedCount ?? 0,
    results: rawResults.map((item) => ({
      seq: item.seq ?? item.Seq ?? 0,
      accepted: item.accepted ?? item.Accepted ?? false,
      reason: item.reason ?? item.Reason ?? null,
      flags: item.flags ?? item.Flags ?? null,
    })),
  }
}

function isRetryableRejection(reason: string | null) {
  return reason === 'rate_limited' || reason === 'session_secret_missing'
}

function reportCollectorError(error: unknown) {
  console.error('Behavioral event flush failed; queued events will be retried.', error)
}

async function signEnvelope(
  sessionId: string,
  seq: number,
  timestampMs: number,
  nonce: string,
  data: string,
  signingKey: CryptoKey,
) {
  const material = [
    sessionId.replaceAll('-', ''),
    String(seq),
    String(timestampMs),
    nonce,
    data,
  ].join('.')
  const signature = await crypto.subtle.sign('HMAC', signingKey, new TextEncoder().encode(material))

  return bytesToBase64(new Uint8Array(signature))
}

async function encryptEventData(data: object, encryptionKey: CryptoKey, nonce: Uint8Array) {
  const encoded = new TextEncoder().encode(JSON.stringify(data))
  const ciphertext = await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv: toArrayBuffer(nonce) },
    encryptionKey,
    toArrayBuffer(encoded),
  )

  return bytesToBase64Url(new Uint8Array(ciphertext))
}

async function deriveBehaviorKeys(sessionSecret: string): Promise<BehaviorCryptoKeys> {
  const baseKey = await crypto.subtle.importKey('raw', toArrayBuffer(decodeSessionSecret(sessionSecret)), 'HKDF', false, ['deriveKey'])
  const salt = new Uint8Array(32)
  const signingInfo = new TextEncoder().encode('signing')
  const encryptionInfo = new TextEncoder().encode('encryption')

  const signingKey = await crypto.subtle.deriveKey(
    { name: 'HKDF', hash: 'SHA-256', salt: toArrayBuffer(salt), info: toArrayBuffer(signingInfo) },
    baseKey,
    { name: 'HMAC', hash: 'SHA-256', length: 256 },
    false,
    ['sign'],
  )

  const encryptionKey = await crypto.subtle.deriveKey(
    { name: 'HKDF', hash: 'SHA-256', salt: toArrayBuffer(salt), info: toArrayBuffer(encryptionInfo) },
    baseKey,
    { name: 'AES-GCM', length: 256 },
    false,
    ['encrypt'],
  )

  return { signingKey, encryptionKey }
}

function decodeSessionSecret(sessionSecret: string) {
  try {
    return base64ToBytes(sessionSecret)
  } catch {
    return new TextEncoder().encode(sessionSecret)
  }
}

function generateNonce() {
  const bytes = new Uint8Array(12)
  crypto.getRandomValues(bytes)
  return bytesToBase64Url(bytes)
}

function bytesToBase64(bytes: Uint8Array) {
  let binary = ''
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte)
  })

  return btoa(binary)
}

function bytesToBase64Url(bytes: Uint8Array) {
  return bytesToBase64(bytes).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '')
}

function base64UrlToBytes(value: string) {
  const normalized = value.replaceAll('-', '+').replaceAll('_', '/')
  return base64ToBytes(normalized.padEnd(normalized.length + ((4 - normalized.length % 4) % 4), '='))
}

function base64ToBytes(value: string) {
  const binary = atob(value)
  const bytes = new Uint8Array(binary.length)
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index)
  }

  return bytes
}

function toArrayBuffer(bytes: Uint8Array) {
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer
}
