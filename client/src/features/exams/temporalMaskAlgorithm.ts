// Pure, DOM-free core of the temporal mask: given a noise field and a letter
// mask (which pixels belong to glyph shapes), derive the grayscale value of
// each of the two alternating frames. Kept separate from TemporalMaskText.tsx
// so the core invariant - frame A and frame B are each independently
// statistically indistinguishable from pure noise, but |A-B| reconstructs
// the mask exactly - can be unit tested without a real <canvas> (jsdom has
// no Canvas2D, so anything touching getContext('2d') can't run there).

export const NOISE_LEVEL_LOW = 64
export const NOISE_LEVEL_HIGH = 192

export function invertLevel(value: number): number {
  return value === NOISE_LEVEL_LOW ? NOISE_LEVEL_HIGH : NOISE_LEVEL_LOW
}

// parity 0 = noise everywhere ("A"); parity 1 = noise outside the text, the
// inverted level inside it ("B").
export function composeFrame(noise: Uint8ClampedArray, letterMask: Uint8Array, parity: 0 | 1): Uint8ClampedArray {
  const out = new Uint8ClampedArray(noise.length)
  for (let i = 0; i < noise.length; i += 1) {
    const base = noise[i]
    out[i] = letterMask[i] === 1 && parity === 1 ? invertLevel(base) : base
  }
  return out
}

export function buildNoise(width: number, height: number, dpr: number, random: () => number = Math.random): Uint8ClampedArray {
  const grain = Math.max(1, Math.round(3 * dpr))
  const noise = new Uint8ClampedArray(width * height)
  for (let by = 0; by < height; by += grain) {
    for (let bx = 0; bx < width; bx += grain) {
      const value = random() < 0.5 ? NOISE_LEVEL_LOW : NOISE_LEVEL_HIGH
      for (let y2 = by; y2 < Math.min(by + grain, height); y2 += 1) {
        for (let x2 = bx; x2 < Math.min(bx + grain, width); x2 += 1) {
          noise[y2 * width + x2] = value
        }
      }
    }
  }
  return noise
}
