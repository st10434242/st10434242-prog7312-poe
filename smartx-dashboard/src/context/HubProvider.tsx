import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { HUB_EVENTS, HubContext, type HubEvent, type HubHandler, type HubStatus } from './hubContext'

// One connection for the whole app, so switching pages never drops it or opens a second one
export function HubProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<HubStatus>('connecting')
  const listeners = useRef(new Map<HubEvent, Set<HubHandler>>())

  useEffect(() => {
    let cancelled = false

    const connection = new HubConnectionBuilder()
      .withUrl('/hub/telemetry')
      // Keeps retrying rather than giving up, so restarting the gateway recovers on its own
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retry) => (retry.previousRetryCount === 0 ? 0 : 5000),
      })
      .build()

    HUB_EVENTS.forEach((event) => {
      connection.on(event, (payload: unknown) => {
        listeners.current.get(event)?.forEach((handler) => handler(payload))
      })
    })

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

  const subscribe = useCallback((event: HubEvent, handler: HubHandler) => {
    let handlers = listeners.current.get(event)

    if (!handlers) {
      handlers = new Set()
      listeners.current.set(event, handlers)
    }

    handlers.add(handler)

    return () => {
      handlers.delete(handler)
    }
  }, [])

  const value = useMemo(() => ({ status, subscribe }), [status, subscribe])

  return <HubContext.Provider value={value}>{children}</HubContext.Provider>
}
