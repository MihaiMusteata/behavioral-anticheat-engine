import { forwardRef, useEffect, useImperativeHandle, useRef } from 'react'
import { heartbeatIntervalMs } from '../behavior/detectors'
import type { BehaviorRecorder } from '../behavior/types'
import { buildNoise, composeFrame } from './temporalMaskAlgorithm'

// Research prototype: hides rendered text from a *single* screen capture while
// staying readable to a human via persistence of vision. Background pixels
// are static black/white-ish noise; pixels inside letter shapes invert every
// flip. Any one frame is statistically identical noise everywhere (no edge
// to detect), but two consecutive frames averaged together converge every
// letter pixel to flat mid-gray while the background stays textured - which
// is exactly what the human eye does automatically at display refresh rate,
// and exactly what a screen *recording* can also do with a trivial 2-frame
// average (see FrameAveragingDemo / captureAveragedDataUrl).
//
// Flipping every rAF tick (~refresh rate, commonly 60Hz) is required for the
// fusion illusion to work at all - the algorithm's core property is that NO
// single frame is legible, so a slower, WCAG-safe flip rate (<3Hz) does not
// degrade gracefully into "slow but readable": it simply never fuses, for
// anyone, at any viewing duration, because there's no partially-legible
// frame to perceive between flips. That unresolved conflict with WCAG 2.3.1
// photosensitive-seizure guidance is exactly why this must stay opt-in per
// exam (antiCaptureMode), overridable per student server-side, and gated by
// an explicit warning before the exam starts - never a silent default.

interface MaskState {
  width: number
  height: number
  noise: Uint8ClampedArray
  letterMask: Uint8Array
  frameA: HTMLCanvasElement
  frameB: HTMLCanvasElement
}

export interface TemporalMaskHandle {
  /** Builds the 2-frame temporal average a recorder/attacker would compute, as a data URL. */
  captureAveragedDataUrl: () => string | null
}

interface TemporalMaskTextProps {
  text: string
  as?: 'h3' | 'p' | 'span' | 'div'
  className?: string
  dataBaeZone?: string
  enabled?: boolean
  record?: BehaviorRecorder
}

const droppedFrameThresholdMs = 33 // ~2x the expected ~16.7ms frame budget at 60Hz

export const TemporalMaskText = forwardRef<TemporalMaskHandle, TemporalMaskTextProps>(function TemporalMaskText(
  { text, as = 'span', className, dataBaeZone, enabled = true, record },
  ref,
) {
  const textRef = useRef<HTMLElement | null>(null)
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const maskStateRef = useRef<MaskState | null>(null)
  const parityRef = useRef(0)

  useEffect(() => {
    if (!enabled) {
      return
    }

    const textEl = textRef.current
    const canvas = canvasRef.current
    if (!textEl || !canvas) {
      return
    }

    function rebuild() {
      const rect = textEl!.getBoundingClientRect()
      if (rect.width === 0 || rect.height === 0) {
        return
      }

      const dpr = window.devicePixelRatio || 1
      const cssWidth = Math.ceil(rect.width)
      const cssHeight = Math.ceil(rect.height)
      canvas!.style.width = `${cssWidth}px`
      canvas!.style.height = `${cssHeight}px`
      canvas!.width = Math.max(1, Math.round(cssWidth * dpr))
      canvas!.height = Math.max(1, Math.round(cssHeight * dpr))

      // Built and swapped in as a single unit so the canvas never shows a
      // half-updated pairing of an old-size frame against a new-size one.
      maskStateRef.current = buildMaskState(text, canvas!.width, canvas!.height, dpr, window.getComputedStyle(textEl!))
      blit(canvas!, maskStateRef.current, parityRef.current)
    }

    rebuild()
    const resizeObserver = new ResizeObserver(rebuild)
    resizeObserver.observe(textEl)

    return () => resizeObserver.disconnect()
  }, [text, enabled])

  useEffect(() => {
    if (!enabled) {
      return
    }

    let rafId = 0
    let lastFlipTimestamp: number | null = null
    let lastHeartbeatTimestamp: number | null = null
    let intervalSum = 0
    let intervalCount = 0
    let droppedFrames = 0

    function tick(timestamp: number) {
      rafId = window.requestAnimationFrame(tick)

      if (lastFlipTimestamp !== null) {
        const intervalMs = timestamp - lastFlipTimestamp
        intervalSum += intervalMs
        intervalCount += 1
        if (intervalMs > droppedFrameThresholdMs) {
          droppedFrames += 1
        }
      } else {
        lastHeartbeatTimestamp = timestamp
      }
      lastFlipTimestamp = timestamp

      parityRef.current = parityRef.current === 0 ? 1 : 0
      const canvas = canvasRef.current
      if (canvas && maskStateRef.current) {
        blit(canvas, maskStateRef.current, parityRef.current)
      }

      if (record && lastHeartbeatTimestamp !== null && timestamp - lastHeartbeatTimestamp >= heartbeatIntervalMs) {
        const avgFrameIntervalMs = intervalCount > 0 ? intervalSum / intervalCount : null
        record('render_stats', {
          renderMode: 'temporal_mask',
          avgFrameIntervalMs,
          droppedFrames,
          estimatedRefreshHz: avgFrameIntervalMs ? 1000 / avgFrameIntervalMs : null,
          devicePixelRatio: window.devicePixelRatio || 1,
        })
        lastHeartbeatTimestamp = timestamp
        intervalSum = 0
        intervalCount = 0
        droppedFrames = 0
      }
    }

    rafId = window.requestAnimationFrame(tick)
    return () => window.cancelAnimationFrame(rafId)
  }, [enabled, record])

  useImperativeHandle(ref, () => ({
    captureAveragedDataUrl: () => {
      const state = maskStateRef.current
      if (!state) {
        return null
      }

      const out = document.createElement('canvas')
      out.width = state.width
      out.height = state.height
      const ctx = out.getContext('2d')
      if (!ctx) {
        return null
      }

      const imageData = ctx.createImageData(state.width, state.height)
      const data = imageData.data
      for (let i = 0; i < state.width * state.height; i += 1) {
        // average(level, inverted-level) = 128 for every letter pixel;
        // background averaged with itself stays exactly where it was.
        const value = state.letterMask[i] === 1 ? 128 : state.noise[i]
        const offset = i * 4
        data[offset] = value
        data[offset + 1] = value
        data[offset + 2] = value
        data[offset + 3] = 255
      }
      ctx.putImageData(imageData, 0, 0)
      return out.toDataURL()
    },
  }))

  const Tag = as

  return (
    <div style={{ position: 'relative', display: 'inline-block', width: '100%' }}>
      <Tag ref={textRef} className={className} data-bae-zone={dataBaeZone} style={enabled ? { color: 'transparent' } : undefined}>
        {text}
      </Tag>
      {enabled ? (
        <canvas ref={canvasRef} aria-hidden="true" style={{ position: 'absolute', inset: 0, pointerEvents: 'none' }} />
      ) : null}
    </div>
  )
})

function buildMaskState(text: string, width: number, height: number, dpr: number, computed: CSSStyleDeclaration): MaskState {
  const letterMask = buildLetterMask(text, width, height, dpr, computed)
  const noise = buildNoise(width, height, dpr)
  const frameA = buildFrameCanvas(width, height, noise, letterMask, 0)
  const frameB = buildFrameCanvas(width, height, noise, letterMask, 1)
  return { width, height, noise, letterMask, frameA, frameB }
}

function buildLetterMask(text: string, width: number, height: number, dpr: number, computed: CSSStyleDeclaration): Uint8Array {
  const letterMask = new Uint8Array(width * height)
  const offscreen = document.createElement('canvas')
  offscreen.width = width
  offscreen.height = height
  const octx = offscreen.getContext('2d')
  if (!octx) {
    return letterMask
  }

  octx.fillStyle = 'black'
  octx.fillRect(0, 0, width, height)
  octx.fillStyle = 'white'
  octx.textBaseline = 'alphabetic'

  const fontSizePx = (parseFloat(computed.fontSize) || 16) * dpr
  const fontWeight = computed.fontWeight || '400'
  const fontFamily = computed.fontFamily || 'sans-serif'
  octx.font = `${fontWeight} ${fontSizePx}px ${fontFamily}`
  const parsedLineHeight = parseFloat(computed.lineHeight)
  const lineHeightPx = (Number.isFinite(parsedLineHeight) ? parsedLineHeight : fontSizePx * 1.2) * dpr

  const words = text.split(/\s+/).filter(Boolean)
  const lines: string[] = []
  let currentLine = ''
  for (const word of words) {
    const candidate = currentLine ? `${currentLine} ${word}` : word
    if (currentLine && octx.measureText(candidate).width > width) {
      lines.push(currentLine)
      currentLine = word
    } else {
      currentLine = candidate
    }
  }
  if (currentLine) {
    lines.push(currentLine)
  }

  let y = fontSizePx
  for (const line of lines) {
    if (y - fontSizePx > height) {
      break
    }
    octx.fillText(line, 0, y)
    y += lineHeightPx
  }

  const imageData = octx.getImageData(0, 0, width, height)
  for (let i = 0; i < width * height; i += 1) {
    letterMask[i] = imageData.data[i * 4] > 128 ? 1 : 0
  }
  return letterMask
}

// Precomputed once per layout so the render loop is just a drawImage blit,
// no per-pixel work. Pixel values themselves come from the pure, unit-tested
// composeFrame (temporalMaskAlgorithm.ts) - this just writes them into RGBA.
function buildFrameCanvas(width: number, height: number, noise: Uint8ClampedArray, letterMask: Uint8Array, parity: 0 | 1): HTMLCanvasElement {
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const ctx = canvas.getContext('2d')
  if (!ctx) {
    return canvas
  }

  const frame = composeFrame(noise, letterMask, parity)
  const imageData = ctx.createImageData(width, height)
  const data = imageData.data
  for (let i = 0; i < frame.length; i += 1) {
    const value = frame[i]
    const offset = i * 4
    data[offset] = value
    data[offset + 1] = value
    data[offset + 2] = value
    data[offset + 3] = 255
  }
  ctx.putImageData(imageData, 0, 0)
  return canvas
}

function blit(canvas: HTMLCanvasElement, state: MaskState, parity: number) {
  const ctx = canvas.getContext('2d')
  if (!ctx) {
    return
  }
  ctx.drawImage(parity === 0 ? state.frameA : state.frameB, 0, 0)
}
