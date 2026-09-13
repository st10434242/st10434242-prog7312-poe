import { Link } from 'react-router-dom'
import { ConnectionIndicator } from '../components/ConnectionIndicator'
import { FleetSummary } from '../components/FleetSummary'
import { HeartbeatWall } from '../components/HeartbeatWall'
import { useFleet } from '../hooks/useFleet'
import './TelemetryPage.css'

export function TelemetryPage() {
  const { tiles, loading, error, status } = useFleet()

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

      {!loading && !error && tiles.length > 0 && <FleetSummary tiles={tiles} />}

      <HeartbeatWall tiles={tiles} loading={loading} error={error} />
    </main>
  )
}
