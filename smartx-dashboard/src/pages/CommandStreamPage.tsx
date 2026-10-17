import { useFleetContext } from '../context/fleetContext'
import './TelemetryPage.css'

// Shell for now, the live stream, overrides and insights get added on top of this
export function CommandStreamPage() {
  const { tiles, loading } = useFleetContext()
  const actuators = tiles.filter((tile) => tile.category === 'Actuator').length

  return (
    <main className="page">
      <header className="telemetry__header">
        <div>
          <p className="telemetry__eyebrow">Module 02</p>
          <h1 className="telemetry__title">Real-Time Command Stream and History</h1>
        </div>
      </header>

      <p className="telemetry__lead">
        {loading
          ? 'Loading the fleet…'
          : `${tiles.length} devices registered, ${actuators} of them can take override commands.`}
      </p>
    </main>
  )
}
