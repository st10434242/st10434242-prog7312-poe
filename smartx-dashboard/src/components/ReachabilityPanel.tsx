import { useEffect, useMemo, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { MeshReroute, MeshSnapshot, Reach } from '../api/types'
import { useSessionState } from '../hooks/useSessionState'
import './ReachabilityPanel.css'

const GATEWAY = 'GATEWAY'

interface Props {
  snapshot: MeshSnapshot
  onSelect: (nodeId: string) => void
}

export function ReachabilityPanel({ snapshot, onSelect }: Props) {
  const [from, setFrom] = useSessionState('topology.reachFrom', GATEWAY)
  const [zone, setZone] = useSessionState('topology.reachZone', '')
  const [reach, setReach] = useState<Reach | null>(null)
  const [reroutes, setReroutes] = useState<MeshReroute[]>([])
  const [error, setError] = useState<string | null>(null)

  const version = snapshot.generatedAtUtc

  // Reach and reroutes are both BFS on the same snapshot, so they are asked for together
  useEffect(() => {
    let cancelled = false

    Promise.all([api.getReach(from, zone || undefined), api.getReroutes()])
      .then(([found, healed]) => {
        if (!cancelled) {
          setReach(found)
          setReroutes(healed)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setReach(null)
          setError(err instanceof ApiError ? err.message : 'Could not work out who is reachable.')
        }
      })

    return () => {
      cancelled = true
    }
  }, [from, zone, version])

  const locations = useMemo(() => new Map(snapshot.nodes.map((node) => [node.nodeId, node.location ?? ''])), [snapshot])

  // Zones come off the location paths, the first two segments are facility and zone
  const zones = useMemo(() => {
    const paths = snapshot.nodes
      .map((node) => node.location?.split('/').slice(0, 2).join('/'))
      .filter((path): path is string => !!path)

    return [...new Set(paths)].sort()
  }, [snapshot])

  const sources = useMemo(
    () => snapshot.nodes.filter((node) => !node.isGateway).map((node) => node.nodeId),
    [snapshot],
  )

  const inZone = (nodeId: string) => !zone || locations.get(nodeId)?.startsWith(`${zone}/`) || locations.get(nodeId) === zone

  // BFS hands nodes back closest first, grouping by hop count shows the rings around the start
  const rings = useMemo(() => {
    const groups = new Map<number, string[]>()

    reach?.connected
      .filter((node) => node.nodeId !== reach.start)
      .forEach((node) => groups.set(node.hops, [...(groups.get(node.hops) ?? []), node.nodeId]))

    return [...groups.entries()].sort(([a], [b]) => a - b)
  }, [reach])

  const shownReroutes = reroutes.filter((reroute) => inZone(reroute.nodeId))
  const connectedCount = reach ? reach.connected.filter((node) => node.nodeId !== reach.start).length : 0

  return (
    <div className="reach">
      <div className="reach__controls">
        <label className="reach__field">
          <span>From</span>
          <select className="input" value={from} onChange={(event) => setFrom(event.target.value)}>
            <option value={GATEWAY}>Gateway</option>
            {sources.map((id) => (
              <option key={id} value={id}>
                {id}
              </option>
            ))}
          </select>
        </label>
        <label className="reach__field">
          <span>Zone</span>
          <select className="input" value={zone} onChange={(event) => setZone(event.target.value)}>
            <option value="">Whole facility</option>
            {zones.map((path) => (
              <option key={path} value={path}>
                {path.split('/').at(-1)}
              </option>
            ))}
          </select>
        </label>
      </div>

      {error && <p className="panel__empty panel__empty--error">{error}</p>}

      {reach && (
        <>
          <ul className="reach__counts">
            <li>
              <span className="reach__dot reach__dot--up" /> {connectedCount} connected
            </li>
            <li>
              <span className="reach__dot reach__dot--down" /> {reach.down.length} down
            </li>
            <li>
              <span className="reach__dot reach__dot--orphaned" /> {reach.orphaned.length} orphaned
            </li>
          </ul>

          {connectedCount === 0 && (
            <p className="reach__note">
              {reach.down.includes(from) ? `${from} is down, so nothing can be reached from it.` : 'Nothing in this zone can be reached from here.'}
            </p>
          )}

          {rings.length > 0 && (
            <div className="reach__section">
              <h3>Connected, by hops from {reach.start === GATEWAY ? 'the gateway' : reach.start}</h3>
              {rings.map(([hops, ids]) => (
                <div key={hops} className="reach__ring">
                  <span className="reach__hops mono">{hops}</span>
                  <NodeChips ids={ids} onSelect={onSelect} />
                </div>
              ))}
            </div>
          )}

          {reach.down.length > 0 && (
            <div className="reach__section">
              <h3>
                <span className="reach__dot reach__dot--down" /> Down
              </h3>
              <NodeChips ids={reach.down} onSelect={onSelect} />
            </div>
          )}

          {reach.orphaned.length > 0 && (
            <div className="reach__section">
              <h3>
                <span className="reach__dot reach__dot--orphaned" /> Orphaned, still running but cut off
              </h3>
              <NodeChips ids={reach.orphaned} onSelect={onSelect} />
            </div>
          )}
        </>
      )}

      <div className="reach__section">
        <h3>Reroutes</h3>
        {shownReroutes.length === 0 ? (
          <p className="reach__note">Nothing is in the way, every node is on its usual route.</p>
        ) : (
          <ul className="reroutes">
            {shownReroutes.map((reroute) => (
              <li key={reroute.nodeId} className="reroutes__item">
                <button type="button" className="reroutes__node mono" onClick={() => onSelect(reroute.nodeId)}>
                  {reroute.nodeId}
                </button>
                <span className="reroutes__detail">
                  {reroute.isOrphaned ? (
                    <span className="reroutes__lost">
                      cut off, every way out is down (it usually hops via <span className="mono">{reroute.oldNextHop}</span>)
                    </span>
                  ) : (
                    <>
                      hop via <strong className="mono">{reroute.newNextHop}</strong>
                      {reroute.newNextHop !== reroute.oldNextHop && (
                        <> instead of <span className="mono">{reroute.oldNextHop}</span></>
                      )}
                      , {reroute.newHops} hop{reroute.newHops === 1 ? '' : 's'}
                      {reroute.newHops !== reroute.oldHops && ` (was ${reroute.oldHops})`}
                    </>
                  )}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

function NodeChips({ ids, onSelect }: { ids: string[]; onSelect: (nodeId: string) => void }) {
  return (
    <div className="reach__chips">
      {ids.map((id) => (
        <button key={id} type="button" className="reach__chip mono" onClick={() => onSelect(id)} title="Trace this node">
          {id === GATEWAY ? 'Gateway' : id}
        </button>
      ))}
    </div>
  )
}
