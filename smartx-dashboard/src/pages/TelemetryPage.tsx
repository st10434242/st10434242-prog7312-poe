import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ConnectionIndicator } from '../components/ConnectionIndicator'
import { useTelemetryHub } from '../hooks/useTelemetryHub'
import type { LivenessEvent, ReadingEvent } from '../api/types'
import './TelemetryPage.css'

export function TelemetryPage() {
  const [readings, setReadings] = useState(0)
  const [last, setLast] = useState<string>('')
  const seen = useRef(0)

  const status = useTelemetryHub({
    onReading: (event: ReadingEvent) => {
      seen.current += 1
      setReadings(seen.current)
      setLast(`${event.nodeId} ${String(event.value)} (${event.band})`)
    },
    onLiveness: (event: LivenessEvent) => {
      setLast(`${event.nodeId} is now ${event.liveness}`)
    },
  })

  return (
    <main className="page">
      <header className="telemetry__header">
        <div>
          <Link to="/" className="telemetry__back">
            Back to pillars
          </Link>
          <h1 className="telemetry__title">Sensor Data Ingestion and Telemetry</h1>
        </div>
        <ConnectionIndicator status={status} />
      </header>

      {/* Temporary readout proving the push pipeline works, replaced by the wall next */}
      <section className="telemetry__feed">
        <div className="telemetry__count mono">{readings}</div>
        <p className="telemetry__last">
          {last ? `Last event: ${last}` : 'Waiting for the first reading'}
        </p>
      </section>
    </main>
  )
}
