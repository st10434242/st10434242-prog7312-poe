import { createContext, useContext, useEffect, useRef } from 'react'

export type HubStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

// Every event the gateway pushes, the provider listens to all of them once and hands them out
export const HUB_EVENTS = [
  'ReadingReceived',
  'LivenessChanged',
  'AlertRaised',
  'EventLogged',
  'CommandIssued',
  'CommandReverted',
  'InsightsUpdated',
  'MeshChanged',
] as const

export type HubEvent = (typeof HUB_EVENTS)[number]

export type HubHandler = (payload: unknown) => void

interface HubContextValue {
  status: HubStatus
  subscribe: (event: HubEvent, handler: HubHandler) => () => void
}

export const HubContext = createContext<HubContextValue | null>(null)

function useHub(): HubContextValue {
  const hub = useContext(HubContext)

  if (!hub) {
    throw new Error('useHub has to be used inside HubProvider')
  }

  return hub
}

export function useHubStatus(): HubStatus {
  return useHub().status
}

// Subscribes for as long as the component is mounted, the handler can change every render without resubscribing
export function useHubEvent<T>(event: HubEvent, handler: (payload: T) => void) {
  const { subscribe } = useHub()
  const latest = useRef(handler)

  useEffect(() => {
    latest.current = handler
  })

  useEffect(() => subscribe(event, (payload) => latest.current(payload as T)), [event, subscribe])
}
