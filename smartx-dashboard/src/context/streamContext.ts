import { createContext, useContext } from 'react'
import type { Alert, Faults, QueueStats, ReadingEvent, TimelineEvent } from '../api/types'

// A reading as it shows in the live feed, the seq gives React a stable key since node+time can repeat
export interface StreamPacket extends ReadingEvent {
  seq: number
}

export interface Stream {
  packets: StreamPacket[]
  alerts: Alert[]
  events: TimelineEvent[]
  stats: QueueStats | null
  faults: Faults | null
}

export const StreamContext = createContext<Stream | null>(null)

export function useStream(): Stream {
  const stream = useContext(StreamContext)

  if (!stream) {
    throw new Error('useStream has to be used inside StreamProvider')
  }

  return stream
}
