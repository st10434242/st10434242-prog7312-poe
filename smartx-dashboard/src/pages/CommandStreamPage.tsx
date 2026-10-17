import { useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { AlertPanel } from '../components/AlertPanel'
import { CommandHistoryPanel } from '../components/CommandHistoryPanel'
import { ControlPanel } from '../components/ControlPanel'
import { EventFeed } from '../components/EventFeed'
import { PacketFeed } from '../components/PacketFeed'
import { StreamStats } from '../components/StreamStats'
import { TimelinePanel } from '../components/TimelinePanel'
import { useFleetContext } from '../context/fleetContext'
import { useStream } from '../context/streamContext'
import { useActuators } from '../hooks/useActuators'
import { useCommandHistory } from '../hooks/useCommandHistory'
import './CommandStreamPage.css'
import './TelemetryPage.css'

export function CommandStreamPage() {
  const { tiles } = useFleetContext()
  const { packets, alerts, events, stats, faults } = useStream()
  const actuators = useActuators()
  const commands = useCommandHistory()

  // Selected device and range live in the URL so a refresh or the back button keeps them
  const [params, setParams] = useSearchParams()
  const rangeParam = params.get('range')
  const rangeMinutes = rangeParam === 'all' ? null : Number(rangeParam ?? 15)

  // Nothing picked yet so start on the first soil sensor, not the latest alert or the chart would keep jumping
  const firstSoil = tiles
    .filter((tile) => tile.category === 'Environmental')
    .sort((a, b) => a.nodeId.localeCompare(b.nodeId))[0]
  const nodeId = params.get('node') ?? firstSoil?.nodeId ?? null

  const categories = useMemo(() => new Map(tiles.map((tile) => [tile.nodeId, tile.category])), [tiles])

  const update = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    next.set(key, value)
    setParams(next, { replace: key === 'range' })
  }

  const select = (id: string) => update('node', id)

  return (
    <main className="page">
      <header className="telemetry__header">
        <div>
          <p className="telemetry__eyebrow">Module 02</p>
          <h1 className="telemetry__title">Real-Time Command Stream and History</h1>
        </div>
      </header>

      <StreamStats stats={stats} faults={faults} />

      <div className="stream-grid">
        <div className="stream-grid__main">
          <TimelinePanel
            tiles={tiles}
            nodeId={nodeId}
            rangeMinutes={rangeMinutes}
            onSelect={select}
            onRange={(minutes) => update('range', minutes === null ? 'all' : String(minutes))}
          />
          <ControlPanel
            devices={actuators.devices}
            loading={actuators.loading}
            error={actuators.error}
            markCommanded={actuators.markCommanded}
            refresh={actuators.refresh}
          />
          <PacketFeed packets={packets} categoryOf={(id) => categories.get(id)} onSelect={select} />
        </div>

        <div className="stream-grid__side">
          <CommandHistoryPanel history={commands.history} refresh={commands.refresh} />
          <AlertPanel alerts={alerts} faults={faults} selected={nodeId} onSelect={select} />
          <EventFeed events={events} />
        </div>
      </div>
    </main>
  )
}
