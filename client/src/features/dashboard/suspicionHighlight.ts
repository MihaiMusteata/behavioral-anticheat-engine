import type { CSSProperties } from 'react'

/**
 * Highlight of a whole question box, proportional to its suspicion score:
 * hue goes green (0) -> amber (0.5) -> red (1), stronger tint as it rises.
 * No score yet (waiting / processing / no result) -> neutral.
 */
export function suspicionHighlight(score: number | null | undefined): CSSProperties {
  if (score === null || score === undefined || Number.isNaN(score)) {
    return {}
  }
  const value = Math.max(0, Math.min(score, 1))
  const hue = Math.round(130 * (1 - value))
  return {
    backgroundColor: `hsl(${hue} 85% ${97 - value * 7}%)`,
    borderColor: `hsl(${hue} 70% ${62 - value * 12}%)`,
    borderLeftWidth: 4,
  }
}
