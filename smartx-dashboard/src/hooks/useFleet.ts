import { useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { LivenessState, SensorCategory, SensorValue, ThresholdBand } from '../api/types'
import { useTelemetryHub, type HubStatus } from './useTelemetryHub'

export interface Tile {
  nodeId: string
  location: string
  category: SensorCategory
  expectedIntervalMs: number
  value: SensorValue | null
  band: ThresholdBand
  liveness: LivenessState
  lastSeenUtc: string | null
}

interface Fleet {
  tiles: Tile[]
  loading: boolean
  error: string | null
  status: HubStatus
}

export function useFleet(): Fleet {
  const [tiles, setTiles] = useState<Map<string, Tile>>(new Map())
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const pending = useRef(new Map<string, Partial<Tile>>())

  useEffect(() => {
    api
      .getSensors()
      .then((sensors) =>
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
              },
            ]),
          ),
        ),
      )
      .catch((problem: Error) => setError(problem.message))
      .finally(() => setLoading(false))
  }, [])

  // Readings arrive faster than anyone can read them, so they are merged and flushed on a timer
  // rather than re-rendering the whole wall once per packet
  useEffect(() => {
    const timer = setInterval(() => {
      if (pending.current.size === 0) {
        return
      }

      const updates = pending.current
      pending.current = new Map()

      setTiles((previous) => {
        const next = new Map(previous)

        updates.forEach((patch, nodeId) => {
          const tile = next.get(nodeId)

          if (tile) {
            next.set(nodeId, { ...tile, ...patch })
          }
        })

        return next
      })
    }, 100)

    return () => clearInterval(timer)
  }, [])

  const status = useTelemetryHub({
    onReading: (event) => {
      pending.current.set(event.nodeId, {
        ...pending.current.get(event.nodeId),
        value: event.value,
        band: event.band,
        // A reading arriving is itself proof of life, the server sends no separate Live event
        liveness: 'Live',
        lastSeenUtc: event.timestampUtc,
      })
    },
    onLiveness: (event) => {
      pending.current.set(event.nodeId, {
        ...pending.current.get(event.nodeId),
        liveness: event.liveness,
        lastSeenUtc: event.lastSeenUtc,
      })
    },
  })

  return { tiles: [...tiles.values()], loading, error, status }
}
