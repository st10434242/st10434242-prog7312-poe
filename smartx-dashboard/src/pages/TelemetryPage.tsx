import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ConnectionIndicator } from '../components/ConnectionIndicator'
import { FleetSummary } from '../components/FleetSummary'
import { HeartbeatWall } from '../components/HeartbeatWall'
import { ZoneNav } from '../components/ZoneNav'
import { useFleet } from '../hooks/useFleet'
import { findZone, useTopology } from '../hooks/useTopology'
import './TelemetryPage.css'

export function TelemetryPage() {
  const { tiles, loading, error, status } = useFleet()
  const root = useTopology(tiles.length)

  // Kept in the URL so the browser's back button walks back up the hierarchy
  const [params, setParams] = useSearchParams()
  const zoneParam = params.get('zone') ?? ''
  const path = useMemo(() => (zoneParam ? zoneParam.split('/') : []), [zoneParam])

  const zone = findZone(root, path)

  const visible = useMemo(() => {
    if (path.length === 0) {
      return tiles
    }

    const prefix = `${path.join('/').toLowerCase()}/`
    return tiles.filter(
      (tile) =>
        tile.location.toLowerCase() === path.join('/').toLowerCase() ||
        tile.location.toLowerCase().startsWith(prefix),
    )
  }, [tiles, path])

  const navigate = (next: string[]) => {
    setParams(next.length === 0 ? {} : { zone: next.join('/') })
  }

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

      <ZoneNav zone={zone} path={path} tiles={tiles} onNavigate={navigate} />

      {!loading && !error && visible.length > 0 && <FleetSummary tiles={visible} />}

      <HeartbeatWall tiles={visible} loading={loading} error={error} />
    </main>
  )
}
