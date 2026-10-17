import { useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { Device, DeviceAlertState, DeviceFilter } from '../api/types'

const DEBOUNCE_MS = 250
const REFRESH_MS = 3000
const ACTIVITY_MS = 1200

export type StateCounts = Record<DeviceAlertState, number>

const emptyCounts: StateCounts = { Normal: 0, Caution: 0, Alert: 0, Disconnected: 0 }

// Filtering happens on the gateway against its device dictionary, the dashboard just asks for what it wants
export function useDeviceSearch(filter: DeviceFilter) {
  const [devices, setDevices] = useState<Device[]>([])
  const [counts, setCounts] = useState<StateCounts>(emptyCounts)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  const { category, alertState, search } = filter
  const lastRecorded = useRef('')

  // States change all the time so the results refresh on their own
  useEffect(() => {
    const timer = setInterval(() => setTick((value) => value + 1), REFRESH_MS)
    return () => clearInterval(timer)
  }, [])

  useEffect(() => {
    let cancelled = false

    // Waits for the typing to stop so every key press isn't a request
    const timer = setTimeout(() => {
      Promise.all([
        api.getDevices({ category, alertState, search }),
        // Same search without the state filter, so each chip can show how many it would match
        api.getDevices({ category, search }),
      ])
        .then(([matches, all]) => {
          if (cancelled) {
            return
          }

          const next = { ...emptyCounts }
          all.forEach((device) => {
            next[device.alertState] += 1
          })

          setDevices(matches)
          setCounts(next)
          setError(null)
        })
        .catch((problem: Error) => !cancelled && setError(problem.message))
        .finally(() => !cancelled && setLoading(false))
    }, DEBOUNCE_MS)

    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  }, [category, alertState, search, tick])

  // Only a search the operator settled on goes to the learner, not every half typed word
  useEffect(() => {
    const term = search?.trim() ?? ''

    if (term.length < 2 || term.toUpperCase() === lastRecorded.current) {
      return
    }

    const timer = setTimeout(() => {
      lastRecorded.current = term.toUpperCase()
      api.recordActivity({ kind: 'Search', term }).catch(() => {})
    }, ACTIVITY_MS)

    return () => clearTimeout(timer)
  }, [search])

  return { devices, counts, loading, error }
}
