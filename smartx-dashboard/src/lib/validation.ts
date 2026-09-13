import type { SensorRegistration } from '../api/types'

// These mirror the rules in SmartX.Core. The server stays the authority and its errors are shown on the form, but checking here means a typo is caught without a round trip.

export const MAX_DEPTH = 8
export const MAX_SEGMENT_LENGTH = 64
export const MIN_INTERVAL_MS = 100
export const MAX_INTERVAL_MS = 900_000

const MAC = /^([0-9a-f]{2}([:-]?)){5}[0-9a-f]{2}$/i
const SEGMENT = /^[A-Za-z0-9_-]+$/

export function validateRegistration(form: SensorRegistration): Record<string, string> {
  const errors: Record<string, string> = {}

  if (!form.nodeId.trim()) {
    errors.nodeId = 'A node id is required.'
  }

  // Deliberately as permissive as the gateway, which strips separators without requiring them to match. A client stricter than the server would reject a registration the gateway accepts.

  const mac = form.mac.trim()

  if (!mac) {
    errors.mac = 'A MAC address is required.'
  } else if (!MAC.test(mac)) {
    errors.mac = 'Expected a MAC address like AA:BB:CC:DD:EE:FF.'
  }

  const segments = form.location.split('/').map((segment) => segment.trim())

  if (!form.location.trim()) {
    errors.location = 'A deployment location is required.'
  } else if (segments.length > MAX_DEPTH) {
    errors.location = `At most ${MAX_DEPTH} levels deep.`
  } else if (segments.some((segment) => !SEGMENT.test(segment))) {
    errors.location = 'Use letters, digits, hyphens and underscores, separated by /.'
  } else if (segments.some((segment) => segment.length > MAX_SEGMENT_LENGTH)) {
    errors.location = `Each level must be ${MAX_SEGMENT_LENGTH} characters or fewer.`
  }

  if (form.expectedIntervalMs < MIN_INTERVAL_MS || form.expectedIntervalMs > MAX_INTERVAL_MS) {
    errors.expectedIntervalMs = `Between ${MIN_INTERVAL_MS} and ${MAX_INTERVAL_MS} ms.`
  }

  return errors
}
