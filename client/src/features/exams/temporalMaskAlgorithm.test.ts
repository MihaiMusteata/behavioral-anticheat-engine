import { describe, expect, it } from 'vitest'
import { NOISE_LEVEL_HIGH, NOISE_LEVEL_LOW, buildNoise, composeFrame } from './temporalMaskAlgorithm'

// Deterministic PRNG so noise fixtures are reproducible across runs.
function mulberry32(seed: number) {
  let state = seed
  return () => {
    state |= 0
    state = (state + 0x6d2b79f5) | 0
    let t = Math.imul(state ^ (state >>> 15), 1 | state)
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

function buildCheckerboardMask(width: number, height: number, blockSize: number): Uint8Array {
  const mask = new Uint8Array(width * height)
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const inLetterBlock = Math.floor(x / blockSize) % 2 === Math.floor(y / blockSize) % 2
      mask[y * width + x] = inLetterBlock ? 1 : 0
    }
  }
  return mask
}

describe('composeFrame', () => {
  const width = 40
  const height = 20
  const noise = buildNoise(width, height, 1, mulberry32(42))
  const mask = buildCheckerboardMask(width, height, 4)

  it('background pixels are identical between frame A and frame B (no boundary leaks outside the mask)', () => {
    const frameA = composeFrame(noise, mask, 0)
    const frameB = composeFrame(noise, mask, 1)

    for (let i = 0; i < noise.length; i += 1) {
      if (mask[i] === 0) {
        expect(frameA[i]).toBe(noise[i])
        expect(frameB[i]).toBe(noise[i])
      }
    }
  })

  it('frame A alone only ever contains the two noise levels - statistically identical to background everywhere, never reveals the mask', () => {
    const frameA = composeFrame(noise, mask, 0)
    for (const value of frameA) {
      expect([NOISE_LEVEL_LOW, NOISE_LEVEL_HIGH]).toContain(value)
    }
    // frame A is pixel-for-pixel just the noise field, regardless of mask.
    expect(Array.from(frameA)).toEqual(Array.from(noise))
  })

  it('frame B alone also only ever contains the two noise levels - same distribution as background, mask not recoverable from B in isolation', () => {
    const frameB = composeFrame(noise, mask, 1)
    for (const value of frameB) {
      expect([NOISE_LEVEL_LOW, NOISE_LEVEL_HIGH]).toContain(value)
    }
  })

  it('|A - B| reconstructs the mask exactly: 0 outside letters, a fixed swing inside them', () => {
    const frameA = composeFrame(noise, mask, 0)
    const frameB = composeFrame(noise, mask, 1)
    const swing = NOISE_LEVEL_HIGH - NOISE_LEVEL_LOW

    for (let i = 0; i < noise.length; i += 1) {
      const diff = Math.abs(frameA[i] - frameB[i])
      expect(diff).toBe(mask[i] === 1 ? swing : 0)
    }
  })

  it('(A + B) / 2 recovers flat mid-level for every letter pixel and the original noise for background - what both a human eye and a 2-frame screen recording compute', () => {
    const frameA = composeFrame(noise, mask, 0)
    const frameB = composeFrame(noise, mask, 1)
    const midLevel = (NOISE_LEVEL_LOW + NOISE_LEVEL_HIGH) / 2

    for (let i = 0; i < noise.length; i += 1) {
      const average = (frameA[i] + frameB[i]) / 2
      expect(average).toBe(mask[i] === 1 ? midLevel : noise[i])
    }
  })
})

describe('buildNoise', () => {
  it('only ever produces the two defined levels', () => {
    const noise = buildNoise(30, 15, 1, mulberry32(7))
    for (const value of noise) {
      expect([NOISE_LEVEL_LOW, NOISE_LEVEL_HIGH]).toContain(value)
    }
  })
})
