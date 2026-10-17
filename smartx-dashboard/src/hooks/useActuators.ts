import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import type { Device, LivenessEvent, ReadingEvent } from '../api/types'
import { useHubEvent } from '../context/hubContext'

const POLL_MS = 3000

// Every valve, pump and relay with its reported and commanded state, kept fresh from the hub plus a slow poll
export function useActuators() {
  const [devices, setDevices] = useState<Device[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)

  const refresh = useCallback(() => setReloadToken((token) => token + 1), [])

  useEffect(() => {
    let cancelled = false

    api
      .getDevices({ category: 'Actuator' })
      .then((found) => {
        if (!cancelled) {
          setDevices(found)
          setError(null)
        }
      })
      .catch((problem: Error) => !cancelled && setError(problem.message))
      .finally(() => !cancelled && setLoading(false))

    return () => {
      cancelled = true
    }
  }, [reloadToken])

  // Picks up commanded states that change on the server, like a fleet wide shut off from another tab
  useEffect(() => {
    const timer = setInterval(refresh, POLL_MS)
    return () => clearInterval(timer)
  }, [refresh])

  useHubEvent('CommandIssued', refresh)
  useHubEvent('CommandReverted', refresh)

  // A valve reporting its state is the device confirming the command, no need to wait for the poll
  useHubEvent<ReadingEvent>('ReadingReceived', (event) => {
    if (typeof event.value !== 'boolean') {
      return
    }

    const isOn = event.value

    setDevices((previous) =>
      previous.some((device) => device.nodeId === event.nodeId && (device.isOn !== isOn || device.liveness !== 'Live'))
        ? previous.map((device) => (device.nodeId === event.nodeId ? { ...device, isOn, liveness: 'Live' } : device))
        : previous,
    )
  })

  useHubEvent<LivenessEvent>('LivenessChanged', (event) => {
    setDevices((previous) =>
      previous.map((device) => (device.nodeId === event.nodeId ? { ...device, liveness: event.liveness } : device)),
    )
  })

  // Shows the new state straight away while the request is still on its way
  const markCommanded = useCallback((nodeIds: string[], state: boolean) => {
    setDevices((previous) =>
      previous.map((device) => (nodeIds.includes(device.nodeId) ? { ...device, commandedState: state } : device)),
    )
  }, [])

  return { devices, loading, error, refresh, markCommanded }
}
