import { useCallback, useEffect, useMemo, useRef } from 'react'
import { BehaviorEventCollector } from './collector'
import { createDefaultDetectors } from './detectors'
import type { BehaviorContext, BehaviorPayload } from './types'

export function useBehaviorCollector(
  sessionId: string | null,
  sessionSecret: string | null,
  enabled: boolean,
  restoreReason?: string | null,
) {
  const collectorRef = useRef<BehaviorEventCollector | null>(null)

  useEffect(() => {
    if (!enabled || !sessionId || !sessionSecret) {
      return undefined
    }

    let collector: BehaviorEventCollector | null = null
    let detectors = createDefaultDetectors(sessionId)
    const startTimer = window.setTimeout(() => {
      const activeCollector = new BehaviorEventCollector(sessionId, sessionSecret)
      collector = activeCollector
      collectorRef.current = activeCollector
      activeCollector.start()
      detectors.forEach((detector) => detector.start(
        (eventType, data) => activeCollector.record(eventType, data),
        { flush: (keepalive = false) => keepalive ? activeCollector.flushKeepalive() : activeCollector.flush() },
      ))

      const startedStorageKey = getStartedStorageKey(sessionId)
      const wasAlreadyStarted = sessionStorage.getItem(startedStorageKey) !== null
      sessionStorage.setItem(startedStorageKey, new Date().toISOString())
      const isResume = Boolean(restoreReason) || wasAlreadyStarted
      activeCollector.record(isResume ? 'session_resume' : 'session_start', {
        reason: restoreReason ?? (wasAlreadyStarted ? 'collector_reinitialized' : 'exam_started'),
        userAgent: navigator.userAgent,
        viewport: `${window.innerWidth}x${window.innerHeight}`,
      })
    }, 0)

    return () => {
      window.clearTimeout(startTimer)
      detectors.forEach((detector) => detector.stop())
      detectors = []

      if (collector) {
        const collectorToStop = collector
        queueMicrotask(() => {
          if (collectorRef.current === collectorToStop) {
            collectorRef.current = null
          }

          void collectorToStop.stop().catch(reportCollectorLifecycleError)
        })
      }
    }
  }, [enabled, sessionId, sessionSecret, restoreReason])

  const record = useCallback((eventType: string, data: BehaviorPayload = {}) => {
    collectorRef.current?.record(eventType, data)
  }, [])
  const setContext = useCallback((context: BehaviorContext) => {
    collectorRef.current?.setContext(context)
  }, [])
  const flush = useCallback((keepalive = false) => {
    const collector = collectorRef.current
    if (!collector) {
      return Promise.resolve()
    }

    return keepalive ? collector.flushKeepalive() : collector.flush()
  }, [])
  const endSession = useCallback(async (reason: string) => {
    const collector = collectorRef.current
    if (!collector) {
      return
    }

    collector.record('session_end', { reason, questionId: null, questionIndex: null })
    await collector.flush()
  }, [])
  const clearSessionState = useCallback(() => {
    if (!sessionId) {
      return
    }

    sessionStorage.removeItem(getStartedStorageKey(sessionId))
    sessionStorage.removeItem(`bae.seq.${sessionId}`)
  }, [sessionId])

  return useMemo(
    () => ({ record, setContext, flush, endSession, clearSessionState }),
    [clearSessionState, endSession, flush, record, setContext],
  )
}

function getStartedStorageKey(sessionId: string) {
  return `bae.started.${sessionId}`
}

function reportCollectorLifecycleError(error: unknown) {
  console.error('Behavioral collector shutdown flush failed.', error)
}
