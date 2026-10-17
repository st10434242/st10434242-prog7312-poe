import { useState } from 'react'
import type { SensorCategory } from '../api/types'
import type { StreamPacket } from '../context/streamContext'
import { clockTime, formatValue } from '../lib/format'

interface Props {
  packets: StreamPacket[]
  categoryOf: (nodeId: string) => SensorCategory | undefined
  onSelect: (nodeId: string) => void
}

// The raw packets coming off the FIFO queue, newest on top
export function PacketFeed({ packets, categoryOf, onSelect }: Props) {
  // Paused keeps a copy of the list so you can actually read it while hundreds a second arrive
  const [frozen, setFrozen] = useState<StreamPacket[] | null>(null)
  const shown = frozen ?? packets

  return (
    <section className="panel">
      <header className="panel__head">
        <h2 className="panel__title">Live packet stream</h2>
        <button
          type="button"
          className="panel__action"
          onClick={() => setFrozen(frozen ? null : packets)}
          aria-pressed={frozen !== null}
        >
          {frozen ? 'Resume' : 'Pause'}
        </button>
      </header>

      {shown.length === 0 ? (
        <p className="panel__empty">Waiting for the first packet…</p>
      ) : (
        <ol className="feed" aria-live="off">
          {shown.map((packet) => {
            const category = categoryOf(packet.nodeId)

            return (
              <li key={packet.seq}>
                <button type="button" className="feed__row" onClick={() => onSelect(packet.nodeId)}>
                  <span className="feed__time mono">{clockTime(packet.timestampUtc, true)}</span>
                  <span className="feed__node mono">{packet.nodeId}</span>
                  <span className="feed__value mono">
                    {category ? formatValue(packet.value, category, packet.nodeId) : String(packet.value)}
                  </span>
                  <span className={`band band--${packet.band.toLowerCase()}`}>{packet.band}</span>
                </button>
              </li>
            )
          })}
        </ol>
      )}
    </section>
  )
}
