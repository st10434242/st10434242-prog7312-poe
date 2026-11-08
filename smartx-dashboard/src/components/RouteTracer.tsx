import { useEffect, useState, type FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { MeshRoute, MeshSnapshot, RoutePath } from '../api/types'
import { useToast } from '../context/toastContext'
import './RouteTracer.css'

const GATEWAY = 'GATEWAY'

interface Props {
  snapshot: MeshSnapshot | null
  route: MeshRoute | null
  error: string | null
  selected: string | null
  onSelect: (nodeId: string) => void
  onClear: () => void
}

export function RouteTracer({ snapshot, route, error, selected, onSelect, onClear }: Props) {
  const toast = useToast()
  const [search, setSearch] = useState('')
  const [searchError, setSearchError] = useState<string | null>(null)
  const [silenced, setSilenced] = useState<string[]>([])
  const [busy, setBusy] = useState<string | null>(null)

  const version = snapshot?.generatedAtUtc

  // The silenced list changes with the mesh, so reload it alongside every snapshot
  useEffect(() => {
    let cancelled = false

    api
      .getSilenced()
      .then((found) => !cancelled && setSilenced(found))
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [version])

  // The gateway resolves ids and MACs in any format, so let it decide what was typed
  const trace = async (event: FormEvent) => {
    event.preventDefault()
    const term = search.trim()

    if (!term) {
      return
    }

    try {
      const found = await api.getRoute(term)
      setSearchError(null)
      setSearch('')
      onSelect(found.nodeId)
    } catch (err) {
      setSearchError(err instanceof ApiError ? err.message : 'Could not trace that node.')
    }
  }

  const toggle = async (nodeId: string) => {
    const isSilenced = silenced.includes(nodeId)
    setBusy(nodeId)

    try {
      await (isSilenced ? api.restoreNode(nodeId) : api.silenceNode(nodeId))
      setSilenced((list) => (isSilenced ? list.filter((id) => id !== nodeId) : [...list, nodeId]))
      toast(isSilenced ? `${nodeId} is back on the air` : `${nodeId} silenced, watch the route heal`, 'info')
    } catch (err) {
      toast(err instanceof ApiError ? err.message : `Could not change ${nodeId}`, 'error')
    } finally {
      setBusy(null)
    }
  }

  // Handy after a demo that knocked out half a zone
  const restoreAll = async () => {
    setBusy('all')

    try {
      await Promise.all(silenced.map((id) => api.restoreNode(id)))
      toast(`${silenced.length} node${silenced.length === 1 ? '' : 's'} back on the air`, 'info')
      setSilenced([])
    } catch (err) {
      toast(err instanceof ApiError ? err.message : 'Could not restore everything', 'error')
    } finally {
      setBusy(null)
    }
  }

  const options = snapshot?.nodes.filter((node) => !node.isGateway) ?? []

  return (
    <div className="tracer">
      <form className="tracer__search" onSubmit={trace}>
        <label className="sr-only" htmlFor="trace-term">
          MAC address or node id
        </label>
        <input
          id="trace-term"
          className="input mono"
          list="trace-options"
          placeholder="MAC or node id"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <datalist id="trace-options">
          {options.map((node) => (
            <option key={node.nodeId} value={node.mac ?? node.nodeId}>
              {node.nodeId}
            </option>
          ))}
        </datalist>
        <button type="submit" className="panel__action">
          Trace
        </button>
        {selected && (
          <button type="button" className="panel__action" onClick={onClear} title="Stop tracing and show the whole layer again">
            Clear
          </button>
        )}
      </form>

      {searchError && <p className="tracer__error">{searchError}</p>}

      {!selected && <p className="panel__empty">Search by MAC or click a node on the map.</p>}

      {selected && error && <p className="panel__empty panel__empty--error">{error}</p>}

      {selected?.toUpperCase() === GATEWAY && !error && (
        <p className="panel__empty">That's the gateway, every route ends here.</p>
      )}

      {/* A silenced relay drops off the route straight away, so this is where it comes back from */}
      {silenced.length > 0 && (
        <div className="tracer__blocked">
          <span>Silenced:</span>
          {silenced.map((id) => (
            <button key={id} type="button" className="panel__action" disabled={busy === id} onClick={() => toggle(id)}>
              Restore {id}
            </button>
          ))}
          {silenced.length > 1 && (
            <button type="button" className="panel__action" disabled={busy === 'all'} onClick={restoreAll}>
              Restore all
            </button>
          )}
        </div>
      )}

      {route && route.nodeId !== GATEWAY && (
        <RouteDetail route={route} silenced={silenced} busy={busy} onToggle={toggle} onSelect={onSelect} />
      )}
    </div>
  )
}

interface DetailProps {
  route: MeshRoute
  silenced: string[]
  busy: string | null
  onToggle: (nodeId: string) => void
  onSelect: (nodeId: string) => void
}

function RouteDetail({ route, silenced, busy, onToggle, onSelect }: DetailProps) {
  const saving =
    route.backbone.totalCost > 0 ? Math.round((1 - route.shortest.totalCost / route.backbone.totalCost) * 100) : 0

  return (
    <>
      <div className="tracer__node">
        <div>
          <strong className="mono">{route.nodeId}</strong>
          {route.mac && <span className="tracer__mac mono">{route.mac}</span>}
        </div>
        <span className={`tracer__state tracer__state--${route.state.toLowerCase()}`}>{route.state}</span>
      </div>

      {!route.reachable && (
        <p className="tracer__notice tracer__notice--alert">
          {route.state === 'Down'
            ? 'This node is down, so it has no route home.'
            : 'Every way home runs through a down node, this one is cut off until something comes back.'}
        </p>
      )}

      {route.rerouted && route.reachable && (
        <p className="tracer__notice">
          Healed around {route.blockedBy.join(', ')}. It normally goes {route.usual.hopCount} hop
          {route.usual.hopCount === 1 ? '' : 's'} for {route.usual.totalCost.toFixed(1)}.
        </p>
      )}

      {route.reachable && (
        <p className="tracer__summary">
          {saving > 0
            ? `The shortest route costs ${saving}% less than following the MST backbone.`
            : 'The backbone already is this node\'s cheapest way home.'}
        </p>
      )}

      {route.reachable && (
        <div className="tracer__columns">
          <HopList title="MST backbone" tone="mst" path={route.backbone} start={route.nodeId} {...{ silenced, busy, onToggle, onSelect }} />
          <HopList title="Shortest (Dijkstra)" tone="short" path={route.shortest} start={route.nodeId} {...{ silenced, busy, onToggle, onSelect }} />
        </div>
      )}
    </>
  )
}

interface HopListProps {
  title: string
  tone: 'mst' | 'short'
  path: RoutePath
  start: string
  silenced: string[]
  busy: string | null
  onToggle: (nodeId: string) => void
  onSelect: (nodeId: string) => void
}

function HopList({ title, tone, path, start, silenced, busy, onToggle, onSelect }: HopListProps) {
  return (
    <section className={`hops hops--${tone}`}>
      <header className="hops__head">
        <h3>
          <span className="hops__dot" aria-hidden="true" />
          {title}
        </h3>
        <span className="mono">
          {path.hopCount} hop{path.hopCount === 1 ? '' : 's'} · {path.totalCost.toFixed(1)}
        </span>
      </header>

      <ol className="hops__list">
        <li className="hops__item hops__item--start">
          <span className="mono">{start}</span>
        </li>
        {path.hops.map((hop) => {
          const relay = hop.to !== GATEWAY
          const isSilenced = silenced.includes(hop.to)

          return (
            <li key={`${hop.from}-${hop.to}`} className="hops__item">
              <span className="hops__link mono">
                {hop.rssiDbm} dBm · {hop.cost.toFixed(1)}
              </span>
              <div className="hops__row">
                {relay ? (
                  <button type="button" className="hops__node mono" onClick={() => onSelect(hop.to)} title="Trace this relay instead">
                    {hop.to}
                  </button>
                ) : (
                  <span className="hops__node mono">Gateway</span>
                )}
                {relay && (
                  <button
                    type="button"
                    className="hops__silence"
                    disabled={busy === hop.to}
                    onClick={() => onToggle(hop.to)}
                    title={isSilenced ? 'Bring this relay back' : 'Knock this relay out to see the route heal'}
                  >
                    {isSilenced ? 'Restore' : 'Silence'}
                  </button>
                )}
              </div>
            </li>
          )
        })}
      </ol>
    </section>
  )
}
