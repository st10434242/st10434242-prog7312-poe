import type { TimelineEvent, TimelineEventKind } from '../api/types'
import { clockTime } from '../lib/format'

const labels: Record<TimelineEventKind, string> = {
  Alert: 'Alert',
  Command: 'Command',
  Undo: 'Undo',
  Disconnected: 'Offline',
  Online: 'Online',
}

// The history feed, read from the gateway's timestamp sorted event log
export function EventFeed({ events }: { events: TimelineEvent[] }) {
  return (
    <section className="panel">
      <header className="panel__head">
        <h2 className="panel__title">History</h2>
        <span className="panel__hint">alerts, commands, dropouts</span>
      </header>

      {events.length === 0 ? (
        <p className="panel__empty">Nothing has happened yet.</p>
      ) : (
        <ol className="events">
          {events.map((event) => (
            <li key={event.id} className="events__row">
              <span className={`events__kind events__kind--${event.kind.toLowerCase()}`}>{labels[event.kind]}</span>
              <span className="events__message">{event.message}</span>
              <span className="events__time mono">{clockTime(event.timestampUtc)}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}
