import type { ZoneSummary } from '../api/types'
import type { Tile } from '../hooks/useFleet'
import './ZoneNav.css'

interface Props {
  zone: ZoneSummary | null
  path: string[]
  tiles: Tile[]
  onNavigate: (path: string[]) => void
}

export function ZoneNav({ zone, path, tiles, onNavigate }: Props) {
  if (!zone) {
    return null
  }

  return (
    <nav className="zonenav" aria-label="Deployment hierarchy">
      <div className="zonenav__crumbs">
        <Crumb label="All nodes" onClick={() => onNavigate([])} current={path.length === 0} />

        {path.map((segment, index) => (
          <span key={segment} className="zonenav__crumbs">
            <span className="zonenav__separator" aria-hidden="true">
              ›
            </span>
            <Crumb
              label={segment}
              onClick={() => onNavigate(path.slice(0, index + 1))}
              current={index === path.length - 1}
            />
          </span>
        ))}
      </div>

      {zone.children.length > 0 && (
        <div className="zonenav__children">
          {zone.children.map((child) => (
            <ZoneChip
              key={child.name}
              child={child}
              tiles={tiles}
              onClick={() => onNavigate([...path, child.name])}
            />
          ))}
        </div>
      )}
    </nav>
  )
}

function Crumb({
  label,
  onClick,
  current,
}: {
  label: string
  onClick: () => void
  current: boolean
}) {
  return (
    <button
      type="button"
      className={`zonenav__crumb ${current ? 'zonenav__crumb--current' : ''}`}
      onClick={current ? undefined : onClick}
      aria-current={current ? 'page' : undefined}
    >
      {label}
    </button>
  )
}

function ZoneChip({
  child,
  tiles,
  onClick,
}: {
  child: ZoneSummary
  tiles: Tile[]
  onClick: () => void
}) {
  // Counts come from live tiles rather than the fetched summary, which is only a structure snapshot
  const inZone = tiles.filter((tile) => child.nodeIds.includes(tile.nodeId) || inSubtree(tile, child))
  const alerting = inZone.filter((tile) => tile.band === 'Alert').length
  const stale = inZone.filter((tile) => tile.liveness === 'Stale').length

  const tone = alerting > 0 ? 'zonechip--alert' : stale > 0 ? 'zonechip--stale' : ''
  const detail = alerting > 0 ? `${alerting} alerting` : stale > 0 ? `${stale} stale` : `${inZone.length}`

  return (
    <button type="button" className={`zonechip ${tone}`} onClick={onClick}>
      {child.name}
      <span className="zonechip__count">{detail}</span>
    </button>
  )
}

function inSubtree(tile: Tile, zone: ZoneSummary): boolean {
  if (zone.nodeIds.includes(tile.nodeId)) {
    return true
  }

  return zone.children.some((child) => inSubtree(tile, child))
}
