import { useRef } from 'react'
import type { KeyboardEvent } from 'react'

export type CalibrationFieldSample = {
  fieldName: string
  characterCount: number
  totalDurationMs: number
  averageCharsPerSecond: number
  correctionCount: number
  keystrokeTimingsJson: string
}

type Timing = { index: number; dwellMs: number; flightMs: number | null }

// Captures typing rhythm (dwell = how long a key is held, flight = gap to
// the next key) for one input field, aggregated into a single sample instead
// of one event per keystroke - see the conversation that led here: emitting
// a behavioral event per key would multiply load through the whole
// Kafka/worker/risk-recalc pipeline for no benefit. The actual key pressed
// is never recorded, only timing, by design (this isn't meant to
// reconstruct what was typed - see CalibrationSample.cs on the backend).
export function useKeystrokeDynamics(fieldName: string) {
  const timingsRef = useRef<Timing[]>([])
  const correctionCountRef = useRef(0)
  const firstKeyDownAtRef = useRef<number | null>(null)
  const lastKeyUpAtRef = useRef<number | null>(null)
  const pendingDownAtRef = useRef<number | null>(null)
  const pendingFlightMsRef = useRef<number | null>(null)

  function reset() {
    timingsRef.current = []
    correctionCountRef.current = 0
    firstKeyDownAtRef.current = null
    lastKeyUpAtRef.current = null
    pendingDownAtRef.current = null
    pendingFlightMsRef.current = null
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.repeat) {
      return
    }

    if (event.key === 'Backspace' || event.key === 'Delete') {
      correctionCountRef.current += 1
      return
    }

    if (event.key.length !== 1) {
      // Modifiers, Tab, Enter, arrows, etc. - not a typed character.
      return
    }

    const downAt = performance.now()
    pendingDownAtRef.current = downAt
    pendingFlightMsRef.current = lastKeyUpAtRef.current === null ? null : downAt - lastKeyUpAtRef.current
    if (firstKeyDownAtRef.current === null) {
      firstKeyDownAtRef.current = downAt
    }
  }

  function onKeyUp(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key.length !== 1 || pendingDownAtRef.current === null) {
      return
    }

    const upAt = performance.now()
    timingsRef.current.push({
      index: timingsRef.current.length,
      dwellMs: Math.round(upAt - pendingDownAtRef.current),
      flightMs: pendingFlightMsRef.current === null ? null : Math.round(pendingFlightMsRef.current),
    })
    lastKeyUpAtRef.current = upAt
    pendingDownAtRef.current = null
    pendingFlightMsRef.current = null
  }

  function getSample(): CalibrationFieldSample | null {
    const timings = timingsRef.current
    if (timings.length === 0 || firstKeyDownAtRef.current === null || lastKeyUpAtRef.current === null) {
      return null
    }

    const totalDurationMs = Math.max(Math.round(lastKeyUpAtRef.current - firstKeyDownAtRef.current), 0)
    const characterCount = timings.length

    return {
      fieldName,
      characterCount,
      totalDurationMs,
      averageCharsPerSecond: totalDurationMs > 0 ? characterCount / (totalDurationMs / 1000) : 0,
      correctionCount: correctionCountRef.current,
      keystrokeTimingsJson: JSON.stringify(timings),
    }
  }

  return { onKeyDown, onKeyUp, getSample, reset }
}
