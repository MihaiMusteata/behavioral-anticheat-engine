import { expect, test, type Page } from '@playwright/test'
import { createWorker } from 'tesseract.js'

// Real browser is required here (not Vitest/jsdom): the thing under test is
// actual Canvas2D glyph rasterization + real requestAnimationFrame timing,
// neither of which jsdom implements. See temporalMaskAlgorithm.test.ts for
// the pure-logic unit coverage of the underlying A/B/M invariant.

const harnessText = 'HELLO WORLD'

async function ocr(buffer: Buffer): Promise<string> {
  const worker = await createWorker('eng')
  try {
    const { data } = await worker.recognize(buffer)
    return data.text.trim().toUpperCase()
  } finally {
    await worker.terminate()
  }
}

async function dataUrlToBuffer(dataUrl: string): Promise<Buffer> {
  const base64 = dataUrl.split(',')[1] ?? ''
  return Buffer.from(base64, 'base64')
}

// A real attacker averaging two recorded frames wouldn't OCR the raw
// grayscale average as-is - they'd do the one obvious cleanup step first:
// threshold it. Our averaged letter pixels land on an exact flat value (128)
// that background noise (64/192) never produces, so a simple equality
// threshold - computed here inside the page via canvas, not by reaching into
// implementation internals - is exactly that one step.
async function thresholdAveragedFrame(page: Page, dataUrl: string): Promise<string> {
  return page.evaluate(async (url) => {
    const img = new Image()
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve()
      img.onerror = () => reject(new Error('image failed to load'))
      img.src = url
    })

    const canvas = document.createElement('canvas')
    canvas.width = img.width
    canvas.height = img.height
    const ctx = canvas.getContext('2d')!
    ctx.drawImage(img, 0, 0)

    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height)
    const data = imageData.data
    for (let i = 0; i < data.length; i += 4) {
      const value = data[i]
      const isTextLevel = value === 128
      const out = isTextLevel ? 0 : 255
      data[i] = out
      data[i + 1] = out
      data[i + 2] = out
    }
    ctx.putImageData(imageData, 0, 0)
    return canvas.toDataURL()
  }, dataUrl)
}

test.describe('temporal mask - OCR resistance', () => {
  test('a single captured frame is not OCR-readable', async ({ page }) => {
    await page.goto(`/?maskHarness=1&text=${encodeURIComponent(harnessText)}`)
    const canvas = page.locator('canvas')
    await expect(canvas).toBeVisible()
    await page.waitForTimeout(300) // let the first mask layout build

    const screenshot = await canvas.screenshot()
    const text = await ocr(screenshot)

    expect(text).not.toContain('HELLO')
    expect(text).not.toContain('WORLD')
  })

  test('the 2-frame temporal average is OCR-readable after thresholding', async ({ page }) => {
    await page.goto(`/?maskHarness=1&text=${encodeURIComponent(harnessText)}`)
    await expect(page.locator('canvas')).toBeVisible()
    await page.waitForTimeout(300)

    const averagedDataUrl = await page.evaluate(() => {
      const globalWindow = window as unknown as { __maskHandle?: { captureAveragedDataUrl: () => string | null } }
      return globalWindow.__maskHandle?.captureAveragedDataUrl() ?? null
    })
    expect(averagedDataUrl).not.toBeNull()

    const thresholded = await thresholdAveragedFrame(page, averagedDataUrl!)
    const buffer = await dataUrlToBuffer(thresholded)
    const text = await ocr(buffer)

    expect(text).toContain('HELLO')
  })
})

test.describe('temporal mask - regression (event flow and selection)', () => {
  test('render_stats fires periodically only when masking is enabled', async ({ page }) => {
    await page.goto(`/?maskHarness=1&text=${encodeURIComponent(harnessText)}&enabled=1`)
    await expect(page.locator('canvas')).toBeVisible()

    await page.waitForFunction(() => {
      const events = (window as unknown as { __maskEvents?: Array<{ eventType: string }> }).__maskEvents ?? []
      return events.some((event) => event.eventType === 'render_stats')
    }, { timeout: 15_000 })

    const statsEvent = await page.evaluate(() => {
      const events = (window as unknown as { __maskEvents?: Array<{ eventType: string; data: Record<string, unknown> }> }).__maskEvents ?? []
      return events.find((event) => event.eventType === 'render_stats')
    })

    expect(statsEvent).toBeTruthy()
    expect(statsEvent!.data).toHaveProperty('renderMode', 'temporal_mask')
    expect(statsEvent!.data).toHaveProperty('avgFrameIntervalMs')
    expect(statsEvent!.data).toHaveProperty('droppedFrames')
    expect(statsEvent!.data).toHaveProperty('estimatedRefreshHz')
    expect(statsEvent!.data).toHaveProperty('devicePixelRatio')
  })

  test('no canvas and no render_stats when masking is disabled - plain text renders normally', async ({ page }) => {
    await page.goto(`/?maskHarness=1&text=${encodeURIComponent(harnessText)}&enabled=0`)
    await expect(page.locator('canvas')).toHaveCount(0)
    await page.waitForTimeout(1000)

    const events = await page.evaluate(() => (window as unknown as { __maskEvents?: unknown[] }).__maskEvents ?? [])
    expect(events).toHaveLength(0)
  })

  test('the real question text stays selectable in the DOM regardless of masking - the text-selection detector keeps working unmodified', async ({ page }) => {
    await page.goto(`/?maskHarness=1&text=${encodeURIComponent(harnessText)}&enabled=1`)
    const zone = page.locator('[data-bae-zone="question_prompt"]')
    await expect(zone).toBeAttached()
    await expect(zone).toHaveText(harnessText)

    const selectedText = await page.evaluate(() => {
      const el = document.querySelector('[data-bae-zone="question_prompt"]')
      if (!el) {
        return null
      }
      const range = document.createRange()
      range.selectNodeContents(el)
      const selection = window.getSelection()
      selection?.removeAllRanges()
      selection?.addRange(range)
      return selection?.toString() ?? null
    })

    expect(selectedText).toBe(harnessText)
  })
})
