import type { Tile } from '../hooks/useFleet'
import { categoryLabel, formatValue } from '../lib/format'
import './HeartbeatWall.css'

interface Props {
  tiles: Tile[]
  loading: boolean
  error: string | null
}

export function HeartbeatWall({ tiles, loading, error }: Props) {
  if (error) {
    return <div className="wall__state wall__state--error">Could not reach the gateway: {error}</div>
  }

  if (loading) {
    return <div className="wall__state">Loading the fleet…</div>
  }

  if (tiles.length === 0) {
    return <div className="wall__state">No sensors registered yet.</div>
  }

  return (
    <div className="wall">
      {tiles.map((tile) => (
        <SensorTile key={tile.nodeId} tile={tile} />
      ))}
    </div>
  )
}

function SensorTile({ tile }: { tile: Tile }) {
  return (
    <article
      className={`tile tile--${tile.band.toLowerCase()} tile--${tile.liveness.toLowerCase()}`}
      aria-label={`${tile.nodeId}, ${formatValue(tile.value, tile.category)}, ${tile.band}, ${tile.liveness}`}
    >
      <div className="tile__head">
        <span className="tile__head-left">
          <span className="tile__pulse" aria-hidden="true" />
          <span className="tile__id mono">{tile.nodeId}</span>
        </span>
        {tile.liveness === 'Stale' ? (
          <span className="tile__stale-mark">stale</span>
        ) : (
          <span className="tile__band">{tile.band}</span>
        )}
      </div>

      <div className="tile__value mono">{formatValue(tile.value, tile.category)}</div>

      <div className="tile__meta">
        <span>{categoryLabel(tile.category)}</span>
        <span className="tile__location">{tile.location}</span>
      </div>
    </article>
  )
}
