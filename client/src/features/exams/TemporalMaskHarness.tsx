import { useEffect, useRef } from 'react'
import { TemporalMaskText, type TemporalMaskHandle } from './TemporalMaskText'

// Test-only harness, reachable at ?maskHarness=1 (mirrors the existing
// ?staff bypass in App.tsx). Exposes the real TemporalMaskText component -
// not a reimplementation - plus its imperative handle and recorded events on
// `window`, so Playwright e2e tests can drive and inspect it without going
// through auth/exam-session setup.
export function TemporalMaskHarness() {
  const params = new URLSearchParams(window.location.search)
  const text = params.get('text') ?? 'HELLO WORLD'
  const handleRef = useRef<TemporalMaskHandle>(null)
  const eventsRef = useRef<Array<{ eventType: string; data: unknown }>>([])

  useEffect(() => {
    const globalWindow = window as unknown as { __maskHandle?: TemporalMaskHandle | null; __maskEvents?: typeof eventsRef.current }
    globalWindow.__maskHandle = handleRef.current
    globalWindow.__maskEvents = eventsRef.current
  }, [])

  return (
    <div style={{ padding: 48, background: '#f1f5f9', minHeight: '100vh' }}>
      <TemporalMaskText
        ref={handleRef}
        as="div"
        text={text}
        className="text-5xl font-bold text-slate-950"
        dataBaeZone="question_prompt"
        enabled={params.get('enabled') !== '0'}
        record={(eventType, data) => {
          eventsRef.current.push({ eventType, data: data ?? {} })
        }}
      />
    </div>
  )
}
