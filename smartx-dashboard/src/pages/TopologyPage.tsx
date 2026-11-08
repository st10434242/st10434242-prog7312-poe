import { useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { HistoryViewer } from '../components/HistoryViewer'
import { MeshMap } from '../components/MeshMap'
import { ReachabilityPanel } from '../components/ReachabilityPanel'
import { RouteTracer } from '../components/RouteTracer'
import { useMesh } from '../hooks/useMesh'
import { useRoute } from '../hooks/useRoute'
import { useSessionState } from '../hooks/useSessionState'
import './CommandStreamPage.css'
import './TelemetryPage.css'
import './TopologyPage.css'

export function TopologyPage() {
  const { snapshot, backbone, shortest, error, loading } = useMesh()

  // The picked node lives in the URL so a refresh or a link from another page lands on it
  const [params, setParams] = useSearchParams()
  const selected = params.get('node')
  const [saved, setSaved] = useSessionState('topology.params', '')
  const query = params.toString()

  // Coming back through the nav has no query, so put the last node and layer back
  useEffect(() => {
    if (query) {
      setSaved(query)
    } else if (saved) {
      setParams(new URLSearchParams(saved), { replace: true })
    }
  }, [query, saved, setSaved, setParams])
  const { route, error: routeError } = useRoute(selected, snapshot?.generatedAtUtc)

  const select = (nodeId: string) => {
    const next = new URLSearchParams(params)
    next.set('node', nodeId)
    setParams(next, { replace: true })
  }

  const nodes = snapshot?.nodes.filter((node) => !node.isGateway) ?? []
  const count = (state: string) => nodes.filter((node) => node.state === state).length

  const stats = [
    { label: 'Nodes up', value: snapshot ? `${count('Up')}` : '—', note: snapshot ? `of ${nodes.length} in the mesh` : '' },
    { label: 'Down', value: snapshot ? `${count('Down')}` : '—', note: 'silent or stale', tone: count('Down') > 0 ? 'alert' : undefined },
    { label: 'Orphaned', value: snapshot ? `${count('Orphaned')}` : '—', note: 'running but cut off', tone: count('Orphaned') > 0 ? 'stale' : undefined },
    { label: 'Links', value: snapshot ? `${snapshot.edges.length}` : '—', note: 'radio links heard' },
    { label: 'Backbone cost', value: backbone ? backbone.totalCost.toFixed(1) : '—', note: backbone ? `${backbone.algorithm} MST, ${backbone.edges.length} links` : '' },
    { label: 'Shortest routes', value: shortest ? shortest.totalCost.toFixed(1) : '—', note: shortest ? `${shortest.algorithm} tree total` : '' },
  ]

  return (
    <main className="page">
      <header className="telemetry__header">
        <div>
          <p className="telemetry__eyebrow">Module 03</p>
          <h1 className="telemetry__title">Network Topology and Mesh Routing</h1>
        </div>
      </header>

      <section className="stream-stats" aria-label="Mesh statistics">
        {stats.map((item) => (
          <div key={item.label} className={`stream-stat ${item.tone ? `stream-stat--${item.tone}` : ''}`}>
            <span className="stream-stat__label">{item.label}</span>
            <span className="stream-stat__value mono">{item.value}</span>
            <span className="stream-stat__note">{item.note}</span>
          </div>
        ))}
      </section>

      {error && <p className="panel__empty panel__empty--error">{error}</p>}

      <div className="topology-grid">
        <div className="topology-grid__main">
          <section className="panel">
            <header className="panel__head">
              <h2 className="panel__title">Mesh map</h2>
              {snapshot && <span className="panel__hint">hover a node or link, click a node to trace it</span>}
            </header>
            {snapshot && snapshot.nodes.length > 0 ? (
              <MeshMap snapshot={snapshot} backbone={backbone} shortest={shortest} selected={selected} onSelect={select} route={route} />
            ) : (
              <p className="panel__empty">
                {loading
                  ? 'Loading the mesh…'
                  : error
                    ? 'The map comes back as soon as the gateway does.'
                    : 'No neighbour reports yet. Start SmartX.Simulator and the map fills in.'}
              </p>
            )}
          </section>

          <section className="panel">
            <header className="panel__head">
              <h2 className="panel__title">Historical log</h2>
              <span className="panel__hint">range queries on each node's AVL tree</span>
            </header>
            <HistoryViewer selected={selected} onSelect={select} />
          </section>
        </div>

        <div className="topology-grid__side">
          <section className="panel">
            <header className="panel__head">
              <h2 className="panel__title">Route tracer</h2>
              <span className="panel__hint">MST backbone against the shortest route</span>
            </header>
            <RouteTracer snapshot={snapshot} route={route} error={routeError} selected={selected} onSelect={select} />
          </section>

          <section className="panel">
            <header className="panel__head">
              <h2 className="panel__title">Reachability</h2>
              <span className="panel__hint">BFS over the live mesh</span>
            </header>
            {snapshot && snapshot.nodes.length > 0 ? (
              <ReachabilityPanel snapshot={snapshot} onSelect={select} />
            ) : (
              <p className="panel__empty">Who is connected, down or cut off.</p>
            )}
          </section>
        </div>

      </div>
    </main>
  )
}
