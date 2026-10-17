import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { api } from '../api/client'
import type { Alert, Faults, QueueStats, ReadingEvent, TimelineEvent } from '../api/types'
import { useHubEvent } from './hubContext'
import { StreamContext, type StreamPacket } from './streamContext'

const MAX_PACKETS = 40
const MAX_ALERTS = 100
const MAX_EVENTS = 60
const POLL_MS = 2000

// Keeps the command stream filling up in the background so nothing is lost while you are on another page
export function StreamProvider({ children }: { children: ReactNode }) {
  const [packets, setPackets] = useState<StreamPacket[]>([])
  const [alerts, setAlerts] = useState<Alert[]>([])
  const [events, setEvents] = useState<TimelineEvent[]>([])
  const [stats, setStats] = useState<QueueStats | null>(null)
  const [faults, setFaults] = useState<Faults | null>(null)

  const pending = useRef<StreamPacket[]>([])
  const seq = useRef(0)

  // Backfill from the gateway so the page isn't empty on first load
  useEffect(() => {
    let cancelled = false

    api
      .getAlerts(MAX_ALERTS)
      .then((recent) => !cancelled && setAlerts(recent))
      .catch(() => {})

    api
      .getEvents(MAX_EVENTS)
      .then((recent) => !cancelled && setEvents(recent))
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [])

  // Stats and faults change constantly but nobody needs them faster than every couple of seconds
  useEffect(() => {
    let cancelled = false

    const poll = () => {
      Promise.all([api.getStats(), api.getFaults()])
        .then(([nextStats, nextFaults]) => {
          if (!cancelled) {
            setStats(nextStats)
            setFaults(nextFaults)
          }
        })
        .catch(() => {})
    }

    poll()
    const timer = setInterval(poll, POLL_MS)

    return () => {
      cancelled = true
      clearInterval(timer)
    }
  }, [])

  // Under load this is hundreds of packets a second, so they get flushed in batches like the wall does
  useEffect(() => {
    const timer = setInterval(() => {
      if (pending.current.length === 0) {
        return
      }

      const incoming = pending.current
      pending.current = []

      setPackets((previous) => [...incoming.reverse(), ...previous].slice(0, MAX_PACKETS))
    }, 250)

    return () => clearInterval(timer)
  }, [])

  useHubEvent<ReadingEvent>('ReadingReceived', (event) => {
    seq.current += 1
    pending.current.push({ ...event, seq: seq.current })

    // No point holding more than the feed will ever show
    if (pending.current.length > MAX_PACKETS) {
      pending.current.splice(0, pending.current.length - MAX_PACKETS)
    }
  })

  useHubEvent<Alert>('AlertRaised', (alert) => {
    setAlerts((previous) => [alert, ...previous].slice(0, MAX_ALERTS))
  })

  useHubEvent<TimelineEvent>('EventLogged', (event) => {
    setEvents((previous) => [event, ...previous].slice(0, MAX_EVENTS))
  })

  const value = useMemo(
    () => ({ packets, alerts, events, stats, faults }),
    [packets, alerts, events, stats, faults],
  )

  return <StreamContext.Provider value={value}>{children}</StreamContext.Provider>
}
