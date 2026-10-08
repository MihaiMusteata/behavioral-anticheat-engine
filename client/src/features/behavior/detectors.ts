import type { BehaviorDetector, BehaviorDetectorControls, BehaviorRecorder } from './types'

const defaultIdleThresholdMs = 30_000
const configuredIdleThresholdMs = Number(import.meta.env.VITE_BEHAVIOR_IDLE_MS)
const idleThresholdMs = Number.isFinite(configuredIdleThresholdMs) && configuredIdleThresholdMs >= 1_000
  ? configuredIdleThresholdMs
  : defaultIdleThresholdMs
const idleCheckIntervalMs = Math.max(1_000, Math.min(15_000, Math.floor(idleThresholdMs / 2)))

export class WindowFocusDetector implements BehaviorDetector {
  name = 'window-focus'
  private record: BehaviorRecorder | null = null
  private blurredAt: number | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    window.addEventListener('blur', this.handleBlur)
    window.addEventListener('focus', this.handleFocus)
  }

  stop() {
    window.removeEventListener('blur', this.handleBlur)
    window.removeEventListener('focus', this.handleFocus)
    this.record = null
  }

  private handleBlur = () => {
    this.blurredAt = Date.now()
    this.record?.('window_blur', { activeElement: document.activeElement?.tagName ?? null })
  }

  private handleFocus = () => {
    const blurDurationMs = this.blurredAt ? Date.now() - this.blurredAt : null
    this.blurredAt = null
    this.record?.('window_focus', { blurDurationMs })
  }
}

export class VisibilityDetector implements BehaviorDetector {
  name = 'visibility'
  private record: BehaviorRecorder | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    document.addEventListener('visibilitychange', this.handleVisibilityChange)
  }

  stop() {
    document.removeEventListener('visibilitychange', this.handleVisibilityChange)
    this.record = null
  }

  private handleVisibilityChange = () => {
    this.record?.('visibility_change', {
      visibilityState: document.visibilityState,
      hidden: document.hidden,
    })
  }
}

export class ClipboardDetector implements BehaviorDetector {
  name = 'clipboard'
  private record: BehaviorRecorder | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    document.addEventListener('copy', this.handleCopy)
    document.addEventListener('cut', this.handleCut)
    document.addEventListener('paste', this.handlePaste)
  }

  stop() {
    document.removeEventListener('copy', this.handleCopy)
    document.removeEventListener('cut', this.handleCut)
    document.removeEventListener('paste', this.handlePaste)
    this.record = null
  }

  private handleCopy = (event: ClipboardEvent) => this.record?.('copy', { textLength: getSelectionLength(event.target) })

  private handleCut = (event: ClipboardEvent) => this.record?.('cut', { textLength: getSelectionLength(event.target) })

  private handlePaste = (event: ClipboardEvent) => {
    const text = event.clipboardData?.getData('text') ?? ''
    this.record?.('paste', { textLength: text.length })
  }
}

export class FullscreenDetector implements BehaviorDetector {
  name = 'fullscreen'
  private record: BehaviorRecorder | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    document.addEventListener('fullscreenchange', this.handleFullscreenChange)
  }

  stop() {
    document.removeEventListener('fullscreenchange', this.handleFullscreenChange)
    this.record = null
  }

  private handleFullscreenChange = () => {
    this.record?.(document.fullscreenElement ? 'fullscreen_enter' : 'fullscreen_exit')
  }
}

export class WindowResizeDetector implements BehaviorDetector {
  name = 'window-resize'
  private record: BehaviorRecorder | null = null
  private lastWidth = window.innerWidth
  private lastHeight = window.innerHeight
  private lastSignalAt = 0

  start(record: BehaviorRecorder) {
    this.record = record
    window.addEventListener('resize', this.handleResize)
  }

  stop() {
    window.removeEventListener('resize', this.handleResize)
    this.record = null
  }

  private handleResize = () => {
    const now = Date.now()
    const widthDelta = window.innerWidth - this.lastWidth
    const heightDelta = window.innerHeight - this.lastHeight
    this.lastWidth = window.innerWidth
    this.lastHeight = window.innerHeight

    if ((Math.abs(widthDelta) > 120 || Math.abs(heightDelta) > 120) && now - this.lastSignalAt > 1500) {
      this.record?.('window_resize', {
        width: window.innerWidth,
        height: window.innerHeight,
        widthDelta,
        heightDelta,
      })
      this.lastSignalAt = now
    }
  }
}

export class ContextMenuDetector implements BehaviorDetector {
  name = 'context-menu'
  private record: BehaviorRecorder | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    document.addEventListener('contextmenu', this.handleContextMenu)
  }

  stop() {
    document.removeEventListener('contextmenu', this.handleContextMenu)
    this.record = null
  }

  private handleContextMenu = (event: MouseEvent) => {
    this.record?.('right_click', { x: event.clientX, y: event.clientY })
  }
}

export class KeyboardShortcutDetector implements BehaviorDetector {
  name = 'keyboard-shortcut'
  private record: BehaviorRecorder | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    window.addEventListener('keydown', this.handleKeyDown, true)
  }

  stop() {
    window.removeEventListener('keydown', this.handleKeyDown, true)
    this.record = null
  }

  private handleKeyDown = (event: KeyboardEvent) => {
    const key = event.key.length === 1 ? event.key.toUpperCase() : event.key
    const shortcut = [
      event.ctrlKey || event.metaKey ? 'Ctrl' : null,
      event.shiftKey ? 'Shift' : null,
      event.altKey ? 'Alt' : null,
      key,
    ].filter(Boolean).join('+')

    const tracked =
      (event.ctrlKey || event.metaKey) && ['C', 'V', 'X', 'P', 'F', 'A'].includes(key) ||
      shortcut === 'Ctrl+Shift+I' ||
      shortcut === 'Ctrl+Shift+J' ||
      key === 'F12'

    if (!tracked) {
      return
    }

    this.record?.('keyboard_shortcut', { shortcut })
    if ((event.ctrlKey || event.metaKey) && key === 'P') {
      this.record?.('print_attempt', { source: 'keyboard_shortcut' })
    }
  }
}

export class PrintDetector implements BehaviorDetector {
  name = 'print'
  private record: BehaviorRecorder | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    window.addEventListener('beforeprint', this.handleBeforePrint)
  }

  stop() {
    window.removeEventListener('beforeprint', this.handleBeforePrint)
    this.record = null
  }

  private handleBeforePrint = () => this.record?.('print_attempt', { source: 'beforeprint' })
}

export class InactivityDetector implements BehaviorDetector {
  name = 'inactivity'
  private record: BehaviorRecorder | null = null
  private lastActivityAt = Date.now()
  private timer: number | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    this.lastActivityAt = Date.now()
    window.addEventListener('pointerdown', this.markActivity)
    window.addEventListener('keydown', this.markActivity)
    window.addEventListener('mousemove', this.markActivity)
    this.timer = window.setInterval(this.checkIdle, idleCheckIntervalMs)
  }

  stop() {
    window.removeEventListener('pointerdown', this.markActivity)
    window.removeEventListener('keydown', this.markActivity)
    window.removeEventListener('mousemove', this.markActivity)
    if (this.timer) {
      window.clearInterval(this.timer)
      this.timer = null
    }
    this.record = null
  }

  private markActivity = () => {
    this.lastActivityAt = Date.now()
  }

  private checkIdle = () => {
    const idleMs = Date.now() - this.lastActivityAt
    if (idleMs >= idleThresholdMs) {
      this.record?.('idle_detected', { idleMs })
      this.lastActivityAt = Date.now()
    }
  }
}

export class DevtoolsHeuristicDetector implements BehaviorDetector {
  name = 'devtools-heuristic'
  private record: BehaviorRecorder | null = null
  private timer: number | null = null
  private lastSignalAt = 0

  start(record: BehaviorRecorder) {
    this.record = record
    this.timer = window.setInterval(this.checkDevtoolsShape, 4000)
  }

  stop() {
    if (this.timer) {
      window.clearInterval(this.timer)
      this.timer = null
    }
    this.record = null
  }

  private checkDevtoolsShape = () => {
    const widthGap = window.outerWidth - window.innerWidth
    const heightGap = window.outerHeight - window.innerHeight
    const now = Date.now()
    if ((widthGap > 180 || heightGap > 180) && now - this.lastSignalAt > 20000) {
      this.record?.('devtools_heuristic_triggered', { widthGap, heightGap })
      this.lastSignalAt = now
    }
  }
}

export class MultiTabDetector implements BehaviorDetector {
  name = 'multi-tab'
  private record: BehaviorRecorder | null = null
  private channel: BroadcastChannel | null = null
  private readonly instanceId = crypto.randomUUID()
  private readonly channelName: string

  constructor(sessionId: string) {
    this.channelName = `behavioral-anticheat-session-${sessionId}`
  }

  start(record: BehaviorRecorder) {
    this.record = record
    if (!('BroadcastChannel' in window)) {
      return
    }

    this.channel = new BroadcastChannel(this.channelName)
    this.channel.addEventListener('message', this.handleMessage)
    this.channel.postMessage({ type: 'hello', instanceId: this.instanceId })
  }

  stop() {
    this.channel?.removeEventListener('message', this.handleMessage)
    this.channel?.close()
    this.channel = null
    this.record = null
  }

  private handleMessage = (event: MessageEvent) => {
    if (event.data?.instanceId === this.instanceId) {
      return
    }

    if (event.data?.type === 'hello') {
      this.record?.('multiple_tabs_detected', { detector: 'broadcast_channel' })
      this.channel?.postMessage({ type: 'ack', instanceId: this.instanceId })
    }

    if (event.data?.type === 'ack') {
      this.record?.('multiple_tabs_detected', { detector: 'broadcast_channel' })
    }
  }
}

export class ConnectionDetector implements BehaviorDetector {
  name = 'connection'
  private record: BehaviorRecorder | null = null
  private offlineAt: number | null = null

  start(record: BehaviorRecorder) {
    this.record = record
    this.offlineAt = navigator.onLine ? null : Date.now()
    window.addEventListener('offline', this.handleOffline)
    window.addEventListener('online', this.handleOnline)
  }

  stop() {
    window.removeEventListener('offline', this.handleOffline)
    window.removeEventListener('online', this.handleOnline)
    this.record = null
  }

  private handleOffline = () => {
    this.offlineAt = Date.now()
  }

  private handleOnline = () => {
    if (this.offlineAt === null) {
      return
    }

    this.record?.('session_resume', {
      reason: 'connection_restored',
      offlineDurationMs: Date.now() - this.offlineAt,
      questionId: null,
      questionIndex: null,
    })
    this.offlineAt = null
  }
}

export class PageLifecycleDetector implements BehaviorDetector {
  name = 'page-lifecycle'
  private record: BehaviorRecorder | null = null
  private controls: BehaviorDetectorControls | null = null
  private pageHideRecorded = false

  start(record: BehaviorRecorder, controls?: BehaviorDetectorControls) {
    this.record = record
    this.controls = controls ?? null
    window.addEventListener('beforeunload', this.handleBeforeUnload)
    window.addEventListener('pagehide', this.handlePageHide)
    window.addEventListener('pageshow', this.handlePageShow)

    const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined
    if (navigation?.type === 'reload') {
      this.record('page_reload')
    }
  }

  stop() {
    window.removeEventListener('beforeunload', this.handleBeforeUnload)
    window.removeEventListener('pagehide', this.handlePageHide)
    window.removeEventListener('pageshow', this.handlePageShow)
    this.record = null
    this.controls = null
  }

  private handleBeforeUnload = () => {
    void this.controls?.flush(true).catch(reportDetectorError)
  }

  private handlePageHide = (event: PageTransitionEvent) => {
    if (this.pageHideRecorded) {
      return
    }

    this.pageHideRecorded = true
    this.record?.('page_unload', { questionId: null, questionIndex: null })
    this.record?.('session_end', {
      reason: event.persisted ? 'page_frozen' : 'page_unload',
      persisted: event.persisted,
      questionId: null,
      questionIndex: null,
    })
    void this.controls?.flush(true).catch(reportDetectorError)
  }

  private handlePageShow = (event: PageTransitionEvent) => {
    if (!event.persisted) {
      return
    }

    this.pageHideRecorded = false
    this.record?.('session_resume', {
      reason: 'back_forward_cache',
      questionId: null,
      questionIndex: null,
    })
  }
}

function getSelectionLength(target: EventTarget | null) {
  if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
    const start = target.selectionStart
    const end = target.selectionEnd
    if (start !== null && end !== null) {
      return Math.abs(end - start)
    }
  }

  return window.getSelection()?.toString().length ?? 0
}

function reportDetectorError(error: unknown) {
  console.error('Behavioral lifecycle flush failed.', error)
}

export function createDefaultDetectors(sessionId: string): BehaviorDetector[] {
  return [
    new WindowFocusDetector(),
    new VisibilityDetector(),
    new ClipboardDetector(),
    new FullscreenDetector(),
    new WindowResizeDetector(),
    new ContextMenuDetector(),
    new KeyboardShortcutDetector(),
    new PrintDetector(),
    new InactivityDetector(),
    new DevtoolsHeuristicDetector(),
    new MultiTabDetector(sessionId),
    new ConnectionDetector(),
    new PageLifecycleDetector(),
  ]
}
