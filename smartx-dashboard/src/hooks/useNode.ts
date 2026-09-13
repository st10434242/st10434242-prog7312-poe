import { useEffect, useRef, useState } from 'react'
import { api } from '../api/client'
import type { LivenessState, Reading, Sensor, ThresholdBand } from '../api/types'
import { useTelemetryHub, type HubStatus } from './useTelemetryHub'

const MAX_ROWS = 60

interface NodeState {
  sensor: Sensor | null
  readings: Reading[]
  band: ThresholdBand
  liveness: LivenessState
  loading: boolean
  error: string | null
  status: HubStatus
}

export function useNode(nodeId: string): NodeState {
  const [sensor, setSensor] = useState<Sensor | null>(null)
  const [readings, setReadings] = useState<Reading[]>([])
  const [band, setBand] = useState<ThresholdBand>('Normal')
  const [liveness, setLiveness] = useState<LivenessState>('Stale')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const pending = useRef<Reading[]>([])

  useEffect(() => {
    let cancelled = false

    Promise.all([api.getSensor(nodeId), api.getHistory(nodeId).catch(() => null)])
      .then(([found, history]) => {
        if (cancelled) {
          return
        }

        setSensor(found)
        setLiveness(found.liveness)
        setReadings(history?.readings ?? [])
      })
      .catch((problem: Error) => !cancelled && setError(problem.message))
      .finally(() => !cancelled && setLoading(false))

    return () => {
      cancelled = true
    }
  }, [nodeId])

  // Same batching as the wall, so a fast node does not re-render the table on every packet
  useEffect(() => {
    const timer = setInterval(() => {
      if (pending.current.length === 0) {
        return
      }

      const incoming = pending.current
      pending.current = []

      setReadings((previous) => [...previous, ...incoming].slice(-MAX_ROWS))
    }, 250)

    return () => clearInterval(timer)
  }, [])

  const status = useTelemetryHub({
    onReading: (event) => {
      if (event.nodeId !== nodeId) {
        return
      }

      pending.current.push({ timestampUtc: event.timestampUtc, value: event.value })
      setBand(event.band)
      setLiveness('Live')
    },
    onLiveness: (event) => {
      if (event.nodeId === nodeId) {
        setLiveness(event.liveness)
      }
    },
  })

  return { sensor, readings, band, liveness, loading, error, status }
}
