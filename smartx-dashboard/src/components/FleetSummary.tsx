import type { Tile } from '../hooks/useFleet'
import './FleetSummary.css'

export function FleetSummary({ tiles }: { tiles: Tile[] }) {
  const alerting = tiles.filter((tile) => tile.band === 'Alert').length
  const caution = tiles.filter((tile) => tile.band === 'Caution').length
  const stale = tiles.filter((tile) => tile.liveness === 'Stale').length
  const calm = alerting === 0 && caution === 0 && stale === 0

  return (
    <section className="summary">
      <p className={`summary__headline ${calm ? 'summary__headline--calm' : ''}`}>
        {headline(tiles.length, alerting, caution, stale)}
      </p>

      <div className="summary__stats">
        <Stat label="Nodes" value={tiles.length} />
        <Stat label="Alerting" value={alerting} tone="alert" />
        <Stat label="Caution" value={caution} tone="caution" />
        <Stat label="Stale" value={stale} tone="stale" />
      </div>
    </section>
  )
}

// The answer to "is anything wrong" in words, before the eye has to scan the grid
function headline(total: number, alerting: number, caution: number, stale: number): string {
  if (total === 0) {
    return 'No sensors registered'
  }

  const problems: string[] = []

  if (alerting > 0) {
    problems.push(`${alerting} alerting`)
  }

  if (caution > 0) {
    problems.push(`${caution} in caution`)
  }

  if (stale > 0) {
    problems.push(`${stale} not reporting`)
  }

  return problems.length === 0
    ? `All ${total} nodes reporting normally`
    : `${problems.join(', ')} of ${total} nodes`
}

function Stat({ label, value, tone }: { label: string; value: number; tone?: string }) {
  // Only a non-zero count earns colour, which keeps a healthy fleet visually silent
  const toneClass = tone && value > 0 ? `stat--${tone}` : ''

  return (
    <div className={`stat ${toneClass}`}>
      <span className="stat__value mono">{value}</span>
      <span className="stat__label">{label}</span>
    </div>
  )
}
