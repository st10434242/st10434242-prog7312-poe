import type { Faults, QueueStats } from '../api/types'

interface Props {
  stats: QueueStats | null
  faults: Faults | null
}

// The numbers that prove the queues are keeping up, a growing depth means the processor is falling behind
export function StreamStats({ stats, faults }: Props) {
  const fill = stats ? Math.round((stats.depth / stats.capacity) * 100) : 0

  const items = [
    { label: 'Queue depth', value: stats ? `${stats.depth}` : '—', note: stats ? `${fill}% of ${stats.capacity.toLocaleString()}` : '' },
    { label: 'Packets / sec', value: stats ? `${stats.packetsPerSecond}` : '—', note: stats ? `${stats.processed.toLocaleString()} processed` : '' },
    { label: 'Priority lane', value: stats ? `${stats.alertDepth}` : '—', note: stats ? `${stats.alertsProcessed} alerts jumped the queue` : '' },
    { label: 'Critical', value: stats ? `${stats.criticalAlerts}` : '—', note: stats ? `${stats.warningAlerts} warning${stats.warningAlerts === 1 ? '' : 's'}` : '', tone: 'alert' },
    { label: 'Disconnected', value: faults ? `${faults.disconnected.length}` : '—', note: faults ? `${faults.activeErrors.length} active error${faults.activeErrors.length === 1 ? '' : 's'}` : '', tone: faults && faults.disconnected.length > 0 ? 'stale' : undefined },
    { label: 'Duplicates blocked', value: faults ? `${faults.suppressedDuplicates}` : '—', note: 'repeat alerts the sets ignored' },
  ]

  return (
    <section className="stream-stats" aria-label="Gateway queue statistics">
      {items.map((item) => (
        <div key={item.label} className={`stream-stat ${item.tone ? `stream-stat--${item.tone}` : ''}`}>
          <span className="stream-stat__label">{item.label}</span>
          <span className="stream-stat__value mono">{item.value}</span>
          <span className="stream-stat__note">{item.note}</span>
        </div>
      ))}
    </section>
  )
}
