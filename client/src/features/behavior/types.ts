export type BehaviorPayload = Record<string, unknown>

export type BehaviorEventDraft = {
  eventType: string
  data: BehaviorPayload
}

export type BehaviorEventEnvelope = {
  sessionId: string
  seq: number
  timestamp: string
  nonce: string
  data: string
  signature: string
}

export type BehaviorRecorder = (eventType: string, data?: BehaviorPayload) => void

export type BehaviorDetectorControls = {
  flush: (keepalive?: boolean) => Promise<void>
}

export type BehaviorDetector = {
  name: string
  start: (record: BehaviorRecorder, controls?: BehaviorDetectorControls) => void
  stop: () => void
}

export type BehaviorContext = {
  questionId?: string | null
  questionIndex?: number | null
}
