import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api } from '../api/client'
import { AttachmentsPanel } from '../components/AttachmentsPanel'
import { Sparkline } from '../components/Sparkline'
import { useNode } from '../hooks/useNode'
import { categoryLabel, formatValue } from '../lib/format'
import './NodeDetailPage.css'
import './TelemetryPage.css'

// Keyed on the node so moving between nodes remounts with fresh state, rather than resetting
// it inside an effect and triggering a cascading render
export function NodeDetailPage() {
  const { nodeId = '' } = useParams()

  return <NodeDetail key={nodeId} nodeId={nodeId} />
}

function NodeDetail({ nodeId }: { nodeId: string }) {
  const navigate = useNavigate()
  const { sensor, readings, band, liveness, loading, error } = useNode(nodeId)
  const [silenced, setSilenced] = useState(false)

  // Read the real state back, otherwise leaving the page and returning resets the button
  // to "Silence" while the gateway is still ignoring the node
  useEffect(() => {
    let cancelled = false

    api
      .getSilenced()
      .then((ids) => {
        if (!cancelled) {
          setSilenced(ids.some((id) => id.toLowerCase() === nodeId.toLowerCase()))
        }
      })
      .catch(() => {
        // Debug endpoint being unavailable should not break the page
      })

    return () => {
      cancelled = true
    }
  }, [nodeId])

  if (loading) {
    return <main className="page">Loading {nodeId}…</main>
  }

  if (error || !sensor) {
    return (
      <main className="page">
        <button type="button" className="node__back" onClick={() => navigate(-1)}>
          Back
        </button>
        <h1 className="node__title">{nodeId}</h1>
        <div className="wall__state wall__state--error">{error ?? 'Sensor not found.'}</div>
      </main>
    )
  }

  const latest = readings.at(-1)
  const values = readings.map((reading) =>
    typeof reading.value === 'boolean' ? (reading.value ? 1 : 0) : reading.value,
  )

  const toggleSilence = async () => {
    await (silenced ? api.restoreNode(nodeId) : api.silenceNode(nodeId))
    setSilenced(!silenced)
  }

  return (
    <main className={`page node--${band.toLowerCase()}`}>
      <header className="telemetry__header">
        <div>
          {/* Goes back rather than to a fixed route, so the zone you drilled from is preserved */}
          <button type="button" className="node__back" onClick={() => navigate(-1)}>
            Back
          </button>
          <h1 className="node__title">{sensor.nodeId}</h1>
        </div>
        <div className="node__badges">
          <span className={`tile__band tile--${band.toLowerCase()}`}>{band}</span>
          <span className="connection__label">{liveness}</span>
        </div>
      </header>

      <section className="node__panel">
        <p className="node__panel-title">Latest reading</p>
        <div className="node__reading">{formatValue(latest?.value ?? null, sensor.category, sensor.nodeId)}</div>
        <Sparkline values={values} category={sensor.category} className="spark--detail" />
      </section>

      <section className="node__panel">
        <p className="node__panel-title">Registration</p>
        <dl className="node__facts">
          <div>
            <dt>MAC address</dt>
            <dd>{sensor.mac}</dd>
          </div>
          <div>
            <dt>Deployment</dt>
            <dd>{sensor.location}</dd>
          </div>
          <div>
            <dt>Category</dt>
            <dd>{categoryLabel(sensor.category, sensor.nodeId)}</dd>
          </div>
          <div>
            <dt>Expected every</dt>
            <dd>{sensor.expectedIntervalMs} ms</dd>
          </div>
          <div>
            <dt>Last seen</dt>
            <dd>{sensor.lastSeenUtc ? new Date(sensor.lastSeenUtc).toLocaleTimeString() : 'never'}</dd>
          </div>
          <div>
            <dt>Registered</dt>
            <dd>{new Date(sensor.registeredAtUtc).toLocaleTimeString()}</dd>
          </div>
        </dl>
      </section>

      <section className="node__panel">
        <p className="node__panel-title">Raw packets ({readings.length})</p>
        <div className="node__scroll">
          <table className="node__table">
            <thead>
              <tr>
                <th>Timestamp</th>
                <th>Value</th>
              </tr>
            </thead>
            <tbody>
              {[...readings].reverse().map((reading, index) => (
                <tr key={`${reading.timestampUtc}-${index}`}>
                  <td>{new Date(reading.timestampUtc).toLocaleTimeString()}</td>
                  <td>{formatValue(reading.value, sensor.category, sensor.nodeId)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <AttachmentsPanel nodeId={nodeId} />

      <section className="node__panel">
        <p className="node__panel-title">Fault injection</p>
        <button type="button" className="node__danger" onClick={toggleSilence}>
          {silenced ? 'Restore this node' : 'Silence this node'}
        </button>
      </section>
    </main>
  )
}
