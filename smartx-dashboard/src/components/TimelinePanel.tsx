import type { SensorCategory } from '../api/types'
import type { Tile } from '../hooks/useFleet'
import { useTimeline } from '../hooks/useTimeline'
import { categoryLabel } from '../lib/format'
import { TimelineChart } from './TimelineChart'

const ranges: { label: string; minutes: number | null }[] = [
  { label: '5 min', minutes: 5 },
  { label: '15 min', minutes: 15 },
  { label: 'All', minutes: null },
]

const groups: { category: SensorCategory; label: string }[] = [
  { category: 'Environmental', label: 'Soil moisture' },
  { category: 'PowerConsumption', label: 'Power meters' },
  { category: 'Actuator', label: 'Actuators' },
]

interface Props {
  tiles: Tile[]
  nodeId: string | null
  rangeMinutes: number | null
  onSelect: (nodeId: string) => void
  onRange: (minutes: number | null) => void
}

export function TimelinePanel({ tiles, nodeId, rangeMinutes, onSelect, onRange }: Props) {
  const tile = tiles.find((candidate) => candidate.nodeId === nodeId)

  return (
    <section className="panel">
      <header className="panel__head panel__head--wrap">
        <h2 className="panel__title">{tile ? `${tile.nodeId} · ${categoryLabel(tile.category, tile.nodeId)}` : 'Device timeline'}</h2>

        <div className="timeline__controls">
          <label className="sr-only" htmlFor="timeline-node">
            Device
          </label>
          <select
            id="timeline-node"
            className="input"
            value={nodeId ?? ''}
            onChange={(event) => onSelect(event.target.value)}
          >
            <option value="" disabled>
              Pick a device
            </option>
            {groups.map((group) => (
              <optgroup key={group.category} label={group.label}>
                {tiles
                  .filter((candidate) => candidate.category === group.category)
                  .sort((a, b) => a.nodeId.localeCompare(b.nodeId))
                  .map((candidate) => (
                    <option key={candidate.nodeId} value={candidate.nodeId}>
                      {candidate.nodeId}
                    </option>
                  ))}
              </optgroup>
            ))}
          </select>

          <div className="segmented" role="group" aria-label="Time range">
            {ranges.map((range) => (
              <button
                key={range.label}
                type="button"
                className="segmented__item"
                aria-pressed={rangeMinutes === range.minutes}
                onClick={() => onRange(range.minutes)}
              >
                {range.label}
              </button>
            ))}
          </div>
        </div>
      </header>

      {tile ? (
        <TimelineBody key={`${tile.nodeId}-${rangeMinutes}`} nodeId={tile.nodeId} category={tile.category} rangeMinutes={rangeMinutes} />
      ) : (
        <p className="panel__empty">Pick a device to see its readings over time.</p>
      )}
    </section>
  )
}

// Keyed by node and range so switching either starts with a clean fetch
function TimelineBody({ nodeId, category, rangeMinutes }: { nodeId: string; category: SensorCategory; rangeMinutes: number | null }) {
  const { points, loading, error } = useTimeline(nodeId, rangeMinutes)

  if (error) {
    return <p className="panel__empty panel__empty--error">{error}</p>
  }

  if (loading) {
    return <div className="chart chart--empty">Loading {nodeId}…</div>
  }

  return <TimelineChart points={points} category={category} nodeId={nodeId} />
}
