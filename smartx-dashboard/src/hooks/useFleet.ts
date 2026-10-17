import { useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { LivenessEvent, LivenessState, ReadingEvent, SensorCategory, SensorValue, ThresholdBand } from '../api/types'
import { useHubEvent, useHubStatus, type HubStatus } from '../context/hubContext'

// Roughly a tile's width in points, past which older readings are not worth drawing
const HISTORY_POINTS = 40

export interface Tile {
  nodeId: string
  location: string
  category: SensorCategory
  expectedIntervalMs: number
  value: SensorValue | null
  band: ThresholdBand
  liveness: LivenessState
  lastSeenUtc: string | null
  history: number[]
}

export interface Fleet {
  tiles: Tile[]
  loading: boolean
  error: string | null
  status: HubStatus
  refresh: () => void
}

export function useFleet(): Fleet {
  const [tiles, setTiles] = useState<Map<string, Tile>>(new Map())
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [reloadToken, setReloadToken] = useState(0)

  const pending = useRef(new Map<string, Partial<Tile>>())
  const pendingHistory = useRef(new Map<string, number[]>())

  useEffect(() => {
    let cancelled = false

    api
      .getSensors()
      .then((sensors) => {
        if (cancelled) {
          return
        }

        setTiles(
          new Map(
            sensors.map((sensor) => [
              sensor.nodeId,
              {
                nodeId: sensor.nodeId,
                location: sensor.location,
                category: sensor.category,
                expectedIntervalMs: sensor.expectedIntervalMs,
                value: null,
                band: 'Normal' as ThresholdBand,
                liveness: sensor.liveness,
                lastSeenUtc: sensor.lastSeenUtc,
                history: [],
              },
            ]),
          ),
        )

        // Seeded from the gateway's ring buffer so tiles have a baseline immediately, rather
        // than waiting the best part of a minute for one to accumulate from the live stream
        sensors.forEach((sensor) => {
          api
            .getHistory(sensor.nodeId)
            .then((history) => {
              if (cancelled) {
                return
              }

              const values = history.readings.map((reading) => toNumber(reading.value))

              setTiles((previous) => {
                const tile = previous.get(sensor.nodeId)

                if (!tile) {
                  return previous
                }

                const next = new Map(previous)
                next.set(sensor.nodeId, {
                  ...tile,
                  history: values.slice(-HISTORY_POINTS),
                  value: history.readings.at(-1)?.value ?? tile.value,
                })

                return next
              })
            })
            .catch(() => {
              // A node with no readings yet is normal, its tile just starts empty
            })
        })
      })
      .catch((problem: Error) => !cancelled && setError(problem.message))
      .finally(() => !cancelled && setLoading(false))

    return () => {
      cancelled = true
    }
  }, [reloadToken])

  // Readings arrive faster than anyone can read them, so they are merged and flushed on a timer
  // rather than re-rendering the whole wall once per packet
  useEffect(() => {
    const timer = setInterval(() => {
      if (pending.current.size === 0) {
        return
      }

      const updates = pending.current
      const appended = pendingHistory.current
      pending.current = new Map()
      pendingHistory.current = new Map()

      setTiles((previous) => {
        const next = new Map(previous)

        updates.forEach((patch, nodeId) => {
          const tile = next.get(nodeId)

          if (!tile) {
            return
          }

          const incoming = appended.get(nodeId)

          next.set(nodeId, {
            ...tile,
            ...patch,
            history: incoming ? [...tile.history, ...incoming].slice(-HISTORY_POINTS) : tile.history,
          })
        })

        return next
      })
    }, 100)

    return () => clearInterval(timer)
  }, [])

  const status = useHubStatus()

  useHubEvent<ReadingEvent>('ReadingReceived', (event) => {
    pending.current.set(event.nodeId, {
      ...pending.current.get(event.nodeId),
      value: event.value,
      band: event.band,
      // A reading arriving is itself proof of life, the server sends no separate Live event
      liveness: 'Live',
      lastSeenUtc: event.timestampUtc,
    })

    pendingHistory.current.set(event.nodeId, [
      ...(pendingHistory.current.get(event.nodeId) ?? []),
      toNumber(event.value),
    ])
  })

  useHubEvent<LivenessEvent>('LivenessChanged', (event) => {
    pending.current.set(event.nodeId, {
      ...pending.current.get(event.nodeId),
      liveness: event.liveness,
      lastSeenUtc: event.lastSeenUtc,
    })
  })

  return {
    tiles: [...tiles.values()],
    loading,
    error,
    status,
    refresh: () => setReloadToken((token) => token + 1),
  }
}

// Valve states plot as 0 and 1 so a boolean stream can share the sparkline
function toNumber(value: SensorValue): number {
  return typeof value === 'boolean' ? (value ? 1 : 0) : value
}
