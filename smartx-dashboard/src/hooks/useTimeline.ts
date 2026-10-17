import { useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { ReadingEvent, TimelinePoint } from '../api/types'
import { useHubEvent } from '../context/hubContext'

const MAX_POINTS = 500

interface Timeline {
  points: TimelinePoint[]
  loading: boolean
  error: string | null
}

// Loads a device's history from the sorted timeline endpoint then keeps adding live readings to it.
// Meant to be used in a component keyed on node and range so it starts fresh when either changes.
export function useTimeline(nodeId: string, rangeMinutes: number | null): Timeline {
  const [points, setPoints] = useState<TimelinePoint[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const pending = useRef<TimelinePoint[]>([])

  useEffect(() => {
    let cancelled = false
    const from = rangeMinutes ? new Date(Date.now() - rangeMinutes * 60_000) : undefined

    api
      .getTimeline(nodeId, from, MAX_POINTS)
      .then((timeline) => !cancelled && setPoints(timeline.points))
      .catch((problem: Error) => !cancelled && setError(problem.message))
      .finally(() => !cancelled && setLoading(false))

    return () => {
      cancelled = true
    }
  }, [nodeId, rangeMinutes])

  useEffect(() => {
    const timer = setInterval(() => {
      if (pending.current.length === 0) {
        return
      }

      const incoming = pending.current
      pending.current = []

      setPoints((previous) => {
        // Readings flushed from a device's backlog can be older than what we have, so keep it in time order
        let next = [...previous, ...incoming].sort((a, b) => Date.parse(a.timestampUtc) - Date.parse(b.timestampUtc))

        if (rangeMinutes) {
          const cutoff = Date.now() - rangeMinutes * 60_000
          next = next.filter((point) => Date.parse(point.timestampUtc) >= cutoff)
        }

        return next.slice(-MAX_POINTS)
      })
    }, 500)

    return () => clearInterval(timer)
  }, [rangeMinutes])

  useHubEvent<ReadingEvent>('ReadingReceived', (event) => {
    if (event.nodeId.toLowerCase() !== nodeId.toLowerCase()) {
      return
    }

    const value = typeof event.value === 'boolean' ? (event.value ? 1 : 0) : event.value
    pending.current.push({ timestampUtc: event.timestampUtc, value })
  })

  return { points, loading, error }
}
