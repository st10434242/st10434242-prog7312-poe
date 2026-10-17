import type { HubStatus } from '../context/hubContext'
import './ConnectionIndicator.css'

const labels: Record<HubStatus, string> = {
  connecting: 'Connecting',
  connected: 'Live',
  reconnecting: 'Reconnecting',
  disconnected: 'Disconnected',
}

export function ConnectionIndicator({ status }: { status: HubStatus }) {
  return (
    // Announced politely so the state change reaches a screen reader without interrupting
    <span className={`connection connection--${status}`} role="status" aria-live="polite">
      <span className="connection__dot" aria-hidden="true" />
      {labels[status]}
    </span>
  )
}
