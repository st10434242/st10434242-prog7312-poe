import type { Alert, AlertSeverity, Faults } from '../api/types'
import { clockTime } from '../lib/format'

const SHOWN = 12

const rank: Record<AlertSeverity, number> = { Critical: 3, Warning: 2, Info: 1 }

interface Props {
  alerts: Alert[]
  faults: Faults | null
  selected: string | null
  onSelect: (nodeId: string) => void
}

// Worst first, then newest, which is the same order the gateway's priority queue handles them in
export function AlertPanel({ alerts, faults, selected, onSelect }: Props) {
  const sorted = [...alerts]
    .sort((a, b) => rank[b.severity] - rank[a.severity] || b.raisedAtUtc.localeCompare(a.raisedAtUtc))
    .slice(0, SHOWN)

  const isActive = (alert: Alert) =>
    alert.kind === 'Disconnected'
      ? (faults?.disconnected.includes(alert.nodeId) ?? false)
      : (faults?.activeErrors.some((error) => error.nodeId === alert.nodeId && error.kind === alert.kind) ?? false)

  return (
    <section className="panel">
      <header className="panel__head">
        <h2 className="panel__title">Priority alerts</h2>
        <span className="panel__hint">{alerts.length} recent</span>
      </header>

      {sorted.length === 0 ? (
        <p className="panel__empty">No alerts yet, everything is in range.</p>
      ) : (
        <ul className="alert-list">
          {sorted.map((alert) => {
            const active = isActive(alert)

            return (
              <li key={alert.id}>
                <button
                  type="button"
                  className={`alert-row alert-row--${alert.severity.toLowerCase()} ${active ? '' : 'alert-row--cleared'} ${selected === alert.nodeId ? 'alert-row--selected' : ''}`}
                  onClick={() => onSelect(alert.nodeId)}
                  title="Show this device on the timeline"
                >
                  <span className={`severity severity--${alert.severity.toLowerCase()}`}>
                    <span aria-hidden="true">{alert.severity === 'Critical' ? '▲' : '●'}</span>
                    {alert.severity}
                  </span>
                  <span className="alert-row__body">
                    <span className="alert-row__message">{alert.message}</span>
                    <span className="alert-row__meta mono">
                      {alert.nodeId} · {clockTime(alert.raisedAtUtc)} · {active ? 'active' : 'cleared'}
                    </span>
                  </span>
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
