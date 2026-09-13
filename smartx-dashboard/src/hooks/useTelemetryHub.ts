import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import type { LivenessEvent, ReadingEvent } from '../api/types'

export type HubStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

interface Handlers {
  onReading?: (event: ReadingEvent) => void
  onLiveness?: (event: LivenessEvent) => void
}

export function useTelemetryHub(handlers: Handlers): HubStatus {
  const [status, setStatus] = useState<HubStatus>('connecting')

  // Held in a ref so a re-render does not tear down and rebuild the connection.
  // Assigned in an effect rather than during render, which concurrent rendering does not allow.
  const latest = useRef(handlers)

  useEffect(() => {
    latest.current = handlers
  })

  useEffect(() => {
    let cancelled = false

    const connection = new HubConnectionBuilder()
      .withUrl('/hub/telemetry')
      // Keeps retrying rather than giving up, so restarting the gateway recovers on its own
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retry) => (retry.previousRetryCount === 0 ? 0 : 5000),
      })
      .build()

    connection.on('ReadingReceived', (event: ReadingEvent) => latest.current.onReading?.(event))
    connection.on('LivenessChanged', (event: LivenessEvent) => latest.current.onLiveness?.(event))

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => setStatus('connected'))
    connection.onclose(() => setStatus('disconnected'))

    connection
      .start()
      .then(() => !cancelled && setStatus('connected'))
      .catch(() => !cancelled && setStatus('disconnected'))

    return () => {
      cancelled = true

      // StrictMode runs effects twice in development, so stopping mid-start has to be safe
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop()
      }
    }
  }, [])

  return status
}
