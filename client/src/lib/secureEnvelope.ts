export type SecureEnvelope = {
  sessionId: string
  seq: number
  timestamp: string
  nonce: string
  data: string
  signature: string
}

export type SessionCryptoKeys = {
  signingKey: CryptoKey
  encryptionKey: CryptoKey
}

export async function deriveSessionKeys(sessionSecret: string): Promise<SessionCryptoKeys> {
  const baseKey = await crypto.subtle.importKey('raw', toArrayBuffer(decodeSessionSecret(sessionSecret)), 'HKDF', false, ['deriveKey'])
  const salt = new Uint8Array(32)
  const signingInfo = new TextEncoder().encode('signing')
  const encryptionInfo = new TextEncoder().encode('encryption')

  const signingKey = await crypto.subtle.deriveKey(
    { name: 'HKDF', hash: 'SHA-256', salt: toArrayBuffer(salt), info: toArrayBuffer(signingInfo) },
    baseKey,
    { name: 'HMAC', hash: 'SHA-256', length: 256 },
    false,
    ['sign'],
  )

  const encryptionKey = await crypto.subtle.deriveKey(
    { name: 'HKDF', hash: 'SHA-256', salt: toArrayBuffer(salt), info: toArrayBuffer(encryptionInfo) },
    baseKey,
    { name: 'AES-GCM', length: 256 },
    false,
    ['encrypt'],
  )

  return { signingKey, encryptionKey }
}

export async function buildEnvelope(sessionId: string, seq: number, payload: unknown, keys: SessionCryptoKeys): Promise<SecureEnvelope> {
  const timestampMs = Date.now()
  const timestamp = new Date(timestampMs).toISOString()
  const nonce = generateNonce()
  const nonceBytes = base64UrlToBytes(nonce)
  const data = await encryptPayload(payload, keys.encryptionKey, nonceBytes)
  const signature = await signEnvelope(sessionId, seq, timestampMs, nonce, data, keys.signingKey)

  return { sessionId, seq, timestamp, nonce, data, signature }
}

export async function signEnvelope(
  sessionId: string,
  seq: number,
  timestampMs: number,
  nonce: string,
  data: string,
  signingKey: CryptoKey,
) {
  const material = [
    sessionId.replaceAll('-', ''),
    String(seq),
    String(timestampMs),
    nonce,
    data,
  ].join('.')
  const signature = await crypto.subtle.sign('HMAC', signingKey, new TextEncoder().encode(material))

  return bytesToBase64(new Uint8Array(signature))
}

export async function encryptPayload(payload: unknown, encryptionKey: CryptoKey, nonce: Uint8Array) {
  const encoded = new TextEncoder().encode(JSON.stringify(payload))
  const ciphertext = await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv: toArrayBuffer(nonce) },
    encryptionKey,
    toArrayBuffer(encoded),
  )

  return bytesToBase64Url(new Uint8Array(ciphertext))
}

export function generateNonce() {
  const bytes = new Uint8Array(12)
  crypto.getRandomValues(bytes)
  return bytesToBase64Url(bytes)
}

function decodeSessionSecret(sessionSecret: string) {
  try {
    return base64ToBytes(sessionSecret)
  } catch {
    return new TextEncoder().encode(sessionSecret)
  }
}

export function bytesToBase64(bytes: Uint8Array) {
  let binary = ''
  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte)
  })

  return btoa(binary)
}

export function bytesToBase64Url(bytes: Uint8Array) {
  return bytesToBase64(bytes).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '')
}

export function base64UrlToBytes(value: string) {
  const normalized = value.replaceAll('-', '+').replaceAll('_', '/')
  return base64ToBytes(normalized.padEnd(normalized.length + ((4 - normalized.length % 4) % 4), '='))
}

export function base64ToBytes(value: string) {
  const binary = atob(value)
  const bytes = new Uint8Array(binary.length)
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index)
  }

  return bytes
}

function toArrayBuffer(bytes: Uint8Array) {
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer
}
