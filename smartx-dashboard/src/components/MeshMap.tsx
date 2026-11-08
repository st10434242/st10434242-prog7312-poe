import { useEffect, useMemo, useRef, useState, type MouseEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { MeshEdge, MeshNode, MeshRoute, MeshSnapshot, Reach, RouteHop, SpanningTree } from '../api/types'
import './MeshMap.css'

export type MapLayer = 'links' | 'backbone' | 'shortest' | 'reach'

const layers: { id: MapLayer; label: string; hint: string }[] = [
  { id: 'links', label: 'All links', hint: 'every radio link a node reported, darker is a stronger signal' },
  { id: 'backbone', label: 'MST backbone', hint: 'the cheapest set of links that keeps every node connected' },
  { id: 'shortest', label: 'Shortest routes', hint: 'each node\'s own cheapest way back to the gateway' },
  { id: 'reach', label: 'BFS reach', hint: 'hops from the gateway, the links BFS used to find each node' },
]

// Pixels per metre of floor, the simulator places nodes in metres
const SCALE = 6
const PADDING = 8
const NODE_RADIUS = 9

// Cost runs from 10 for a strong link to 200 for one that barely works
const BEST_COST = 10
const WORST_COST = 200

interface Props {
  snapshot: MeshSnapshot
  backbone: SpanningTree | null
  shortest: SpanningTree | null
  selected?: string | null
  onSelect?: (nodeId: string) => void
  route?: MeshRoute | null
}

type Hover = { kind: 'node'; node: MeshNode } | { kind: 'edge'; edge: MeshEdge }

const keyFor = (a: string, b: string) => (a < b ? `${a}|${b}` : `${b}|${a}`)

// Log scale so the difference between good links still shows, not just good versus terrible
function strength(cost: number) {
  const scaled = (Math.log(WORST_COST) - Math.log(cost)) / (Math.log(WORST_COST) - Math.log(BEST_COST))
  return Math.min(1, Math.max(0, scaled))
}

export function MeshMap({ snapshot, backbone, shortest, selected = null, onSelect, route = null }: Props) {
  // Layer sits in the URL next to the picked node so a shared link opens on the same view
  const [params, setParams] = useSearchParams()
  const layer = layers.find((item) => item.id === params.get('layer'))?.id ?? 'links'

  const setLayer = (next: MapLayer) => {
    const updated = new URLSearchParams(params)
    updated.set('layer', next)
    setParams(updated, { replace: true })
  }
  const [reach, setReach] = useState<Reach | null>(null)
  const [hover, setHover] = useState<Hover | null>(null)
  const [pointer, setPointer] = useState({ x: 0, y: 0, width: 0 })
  const wrapper = useRef<HTMLDivElement>(null)

  // Only asks for BFS when that layer is showing, and again whenever the mesh changes under it
  useEffect(() => {
    if (layer !== 'reach') {
      return
    }

    let cancelled = false

    api
      .getReach()
      .then((found) => !cancelled && setReach(found))
      .catch(() => !cancelled && setReach(null))

    return () => {
      cancelled = true
    }
  }, [layer, snapshot.generatedAtUtc])

  const nodesById = useMemo(() => new Map(snapshot.nodes.map((node) => [node.nodeId, node])), [snapshot])

  const bounds = useMemo(() => {
    const xs = snapshot.nodes.map((node) => node.x)
    const ys = snapshot.nodes.map((node) => node.y)

    return {
      minX: Math.min(...xs) - PADDING,
      minY: Math.min(...ys) - PADDING,
      width: Math.max(...xs) - Math.min(...xs) + (2 * PADDING),
      height: Math.max(...ys) - Math.min(...ys) + (2 * PADDING),
    }
  }, [snapshot])

  const px = (metres: number, origin: number) => (metres - origin) * SCALE
  const sx = (node: MeshNode) => px(node.x, bounds.minX)
  const sy = (node: MeshNode) => px(node.y, bounds.minY)

  // Draws a faint box around each zone so the floor plan reads like the facility
  const zones = useMemo(() => {
    const groups = new Map<string, MeshNode[]>()

    snapshot.nodes.forEach((node) => {
      const zone = node.location?.split('/').slice(0, 2).join('/')

      if (zone) {
        groups.set(zone, [...(groups.get(zone) ?? []), node])
      }
    })

    return [...groups.entries()].map(([path, members]) => ({
      name: path.split('/').at(-1) ?? path,
      minX: Math.min(...members.map((node) => node.x)) - 3,
      minY: Math.min(...members.map((node) => node.y)) - 3,
      maxX: Math.max(...members.map((node) => node.x)) + 3,
      maxY: Math.max(...members.map((node) => node.y)) + 3,
    }))
  }, [snapshot])

  const treeEdges = useMemo(() => {
    const tree = layer === 'backbone' ? backbone : layer === 'shortest' ? shortest : null

    if (layer === 'reach') {
      return new Set(reach?.connected.filter((n) => n.via).map((n) => keyFor(n.nodeId, n.via!)) ?? [])
    }

    return tree ? new Set(tree.edges.map((edge) => keyFor(edge.from, edge.to))) : null
  }, [layer, backbone, shortest, reach])

  const hops = useMemo(() => new Map(reach?.connected.map((n) => [n.nodeId, n.hops]) ?? []), [reach])

  const degree = useMemo(() => {
    const counts = new Map<string, number>()

    snapshot.edges.forEach((edge) => {
      counts.set(edge.from, (counts.get(edge.from) ?? 0) + 1)
      counts.set(edge.to, (counts.get(edge.to) ?? 0) + 1)
    })

    return counts
  }, [snapshot])

  const track = (event: MouseEvent) => {
    const rect = wrapper.current?.getBoundingClientRect()

    if (rect) {
      setPointer({ x: event.clientX - rect.left, y: event.clientY - rect.top, width: rect.width })
    }
  }

  const isDown = (id: string) => nodesById.get(id)?.state === 'Down'

  const edgeClass = (edge: MeshEdge) => {
    if (isDown(edge.from) || isDown(edge.to)) {
      return 'mesh-map__edge mesh-map__edge--dead'
    }

    if (!treeEdges) {
      return 'mesh-map__edge'
    }

    return treeEdges.has(keyFor(edge.from, edge.to)) ? 'mesh-map__edge mesh-map__edge--tree' : 'mesh-map__edge mesh-map__edge--faded'
  }

  const tracing = route?.reachable ?? false

  const routeLines = (hops: RouteHop[], tone: 'mst' | 'short') =>
    hops.map((hop) => {
      const from = nodesById.get(hop.from)
      const to = nodesById.get(hop.to)

      return from && to ? (
        <line
          key={`${tone}-${hop.from}-${hop.to}`}
          className={`mesh-map__route mesh-map__route--${tone}`}
          x1={sx(from)}
          y1={sy(from)}
          x2={sx(to)}
          y2={sy(to)}
        />
      ) : null
    })

  const card = hover ? <HoverCard hover={hover} degree={degree} hops={layer === 'reach' ? hops : null} /> : null

  // Flips the card to the left of the pointer near the right edge so it never gets cut off
  const flip = pointer.x > pointer.width - 240
  const active = layers.find((item) => item.id === layer)!

  return (
    <div className="mesh-map">
      <div className="mesh-map__toolbar">
        <div className="segmented" role="group" aria-label="Map layer">
          {layers.map((item) => (
            <button
              key={item.id}
              type="button"
              className="segmented__item"
              aria-pressed={layer === item.id}
              onClick={() => setLayer(item.id)}
            >
              {item.label}
            </button>
          ))}
        </div>
        <span className="panel__hint">{active.hint}</span>
      </div>

      <div className={`mesh-map__canvas ${tracing ? 'mesh-map__canvas--tracing' : ''}`} ref={wrapper} onMouseMove={track}>
        <svg
          viewBox={`0 0 ${bounds.width * SCALE} ${bounds.height * SCALE}`}
          role="img"
          aria-label={`Mesh map with ${snapshot.nodes.length} nodes and ${snapshot.edges.length} links`}
        >
          {zones.map((zone) => (
            <g key={zone.name}>
              <rect
                className="mesh-map__zone"
                x={px(zone.minX, bounds.minX)}
                y={px(zone.minY, bounds.minY)}
                width={(zone.maxX - zone.minX) * SCALE}
                height={(zone.maxY - zone.minY) * SCALE}
                rx={6}
              />
              <text className="mesh-map__zone-label" x={px(zone.minX, bounds.minX) + 2} y={px(zone.minY, bounds.minY) - 6}>
                {zone.name}
              </text>
            </g>
          ))}

          {snapshot.edges.map((edge) => {
            const from = nodesById.get(edge.from)
            const to = nodesById.get(edge.to)

            if (!from || !to) {
              return null
            }

            const shade = treeEdges ? undefined : 0.12 + (0.75 * strength(edge.cost))

            return (
              <g
                key={keyFor(edge.from, edge.to)}
                onMouseEnter={() => setHover({ kind: 'edge', edge })}
                onMouseLeave={() => setHover(null)}
              >
                <line className={edgeClass(edge)} x1={sx(from)} y1={sy(from)} x2={sx(to)} y2={sy(to)} strokeOpacity={shade} />
                {/* A wider invisible line so thin links are still easy to hover */}
                <line className="mesh-map__hit" x1={sx(from)} y1={sy(from)} x2={sx(to)} y2={sy(to)} />
              </g>
            )
          })}

          {/* Backbone goes underneath and wider so where the two routes share a link both still show */}
          {tracing && route && (
            <g pointerEvents="none">
              {routeLines(route.backbone.hops, 'mst')}
              {routeLines(route.shortest.hops, 'short')}
            </g>
          )}

          {snapshot.nodes.map((node) => {
            const hop = layer === 'reach' ? hops.get(node.nodeId) : undefined

            return (
              <g
                key={node.nodeId}
                className={`mesh-map__node mesh-map__node--${node.isGateway ? 'gateway' : node.state.toLowerCase()}`}
                transform={`translate(${sx(node)} ${sy(node)})`}
                onMouseEnter={() => setHover({ kind: 'node', node })}
                onMouseLeave={() => setHover(null)}
                onClick={() => onSelect?.(node.nodeId)}
              >
                {node.nodeId === selected && <circle className="mesh-map__selected" r={NODE_RADIUS + 5} />}
                {node.isGateway ? (
                  <rect x={-NODE_RADIUS - 2} y={-NODE_RADIUS - 2} width={(NODE_RADIUS + 2) * 2} height={(NODE_RADIUS + 2) * 2} rx={4} />
                ) : (
                  <circle r={NODE_RADIUS} />
                )}
                {hop !== undefined && !node.isGateway && (
                  <text className="mesh-map__hop" dy="0.35em">
                    {hop}
                  </text>
                )}
                {node.isGateway && (
                  <text className="mesh-map__label" y={NODE_RADIUS + 16}>
                    Gateway
                  </text>
                )}
              </g>
            )
          })}
        </svg>

        {card && (
          <div
            className="mesh-map__card"
            style={{ left: flip ? pointer.x - 232 : pointer.x + 14, top: pointer.y + 14 }}
          >
            {card}
          </div>
        )}
      </div>

      <MapLegend tracing={tracing} />
    </div>
  )
}

function HoverCard({ hover, degree, hops }: { hover: Hover; degree: Map<string, number>; hops: Map<string, number> | null }) {
  if (hover.kind === 'edge') {
    const { edge } = hover

    return (
      <>
        <strong className="mono">
          {edge.from} ↔ {edge.to}
        </strong>
        <dl>
          <dt>Signal</dt>
          <dd className="mono">{edge.rssiDbm} dBm</dd>
          <dt>Link cost</dt>
          <dd className="mono">{edge.cost.toFixed(2)}</dd>
        </dl>
      </>
    )
  }

  const { node } = hover
  const hop = hops?.get(node.nodeId)

  return (
    <>
      <strong className="mono">{node.isGateway ? 'Gateway' : node.nodeId}</strong>
      <dl>
        {node.mac && (
          <>
            <dt>MAC</dt>
            <dd className="mono">{node.mac}</dd>
          </>
        )}
        {node.location && (
          <>
            <dt>Location</dt>
            <dd>{node.location}</dd>
          </>
        )}
        <dt>State</dt>
        <dd className={`mesh-map__state mesh-map__state--${node.state.toLowerCase()}`}>{node.state}</dd>
        <dt>Links</dt>
        <dd className="mono">{degree.get(node.nodeId) ?? 0}</dd>
        {hop !== undefined && (
          <>
            <dt>Hops</dt>
            <dd className="mono">{hop}</dd>
          </>
        )}
      </dl>
    </>
  )
}

function MapLegend({ tracing }: { tracing: boolean }) {
  return (
    <ul className="mesh-map__legend">
      <li>
        <span className="mesh-map__swatch mesh-map__swatch--gateway" /> Gateway
      </li>
      <li>
        <span className="mesh-map__swatch mesh-map__swatch--up" /> Up
      </li>
      <li>
        <span className="mesh-map__swatch mesh-map__swatch--down" /> Down
      </li>
      <li>
        <span className="mesh-map__swatch mesh-map__swatch--orphaned" /> Orphaned
      </li>
      <li>
        <span className="mesh-map__strength" /> Weak to strong signal
      </li>
      <li>
        <span className="mesh-map__line mesh-map__line--tree" /> Tree link
      </li>
      <li>
        <span className="mesh-map__line mesh-map__line--dead" /> Link to a down node
      </li>
      {tracing && (
        <>
          <li>
            <span className="mesh-map__line mesh-map__line--mst" /> MST route
          </li>
          <li>
            <span className="mesh-map__line mesh-map__line--short" /> Shortest route
          </li>
        </>
      )}
    </ul>
  )
}
