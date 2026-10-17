import { api } from '../api/client'
import type { DeviceAlertState, Insight, SensorCategory } from '../api/types'
import { useDeviceSearch } from '../hooks/useDeviceSearch'
import { useSessionState } from '../hooks/useSessionState'
import { categoryLabel, formatValue } from '../lib/format'

const categories: { value: SensorCategory | ''; label: string }[] = [
  { value: '', label: 'All categories' },
  { value: 'Environmental', label: 'Soil moisture' },
  { value: 'PowerConsumption', label: 'Power meters' },
  { value: 'Actuator', label: 'Valves, pumps, relays' },
]

// Worst first so the ones that need attention are the first chips you see
const states: DeviceAlertState[] = ['Alert', 'Caution', 'Disconnected', 'Normal']

const stateLabels: Record<DeviceAlertState, string> = {
  Alert: 'Alerting',
  Caution: 'Caution',
  Disconnected: 'Offline',
  Normal: 'Normal',
}

interface Props {
  selected: string | null
  flagged: Map<string, Insight>
  onSelect: (nodeId: string) => void
}

export function DeviceFinder({ selected, flagged, onSelect }: Props) {
  const [search, setSearch] = useSessionState('finder.search', '')
  const [category, setCategory] = useSessionState<SensorCategory | ''>('finder.category', '')
  const [alertState, setAlertState] = useSessionState<DeviceAlertState | ''>('finder.state', '')

  const { devices, counts, loading, error } = useDeviceSearch({
    search: search.trim() || undefined,
    category: category || undefined,
    alertState: alertState || undefined,
  })

  const filtered = search.trim() !== '' || category !== '' || alertState !== ''

  // Devices the engine flagged go to the top, so they are in front of you before you search for them
  const ordered = [...devices].sort((a, b) => (flagged.get(b.nodeId)?.score ?? 0) - (flagged.get(a.nodeId)?.score ?? 0))

  const open = (nodeId: string) => {
    onSelect(nodeId)
    // Opening a device counts as looking at it, which is what the learner wants to know about
    api.recordActivity({ kind: 'View', term: nodeId }).catch(() => {})
  }

  const clear = () => {
    setSearch('')
    setCategory('')
    setAlertState('')
  }

  return (
    <section className="panel">
      <header className="panel__head">
        <h2 className="panel__title">Find a device</h2>
        {filtered && (
          <button type="button" className="panel__action" onClick={clear}>
            Clear
          </button>
        )}
      </header>

      <div className="finder__inputs">
        <label className="sr-only" htmlFor="finder-search">
          Search by node id or location
        </label>
        <input
          id="finder-search"
          className="input finder__search"
          type="search"
          placeholder="Node id or location, e.g. PUMP or Zone2"
          value={search}
          maxLength={64}
          onChange={(event) => setSearch(event.target.value)}
          onKeyDown={(event) => {
            // Enter jumps straight to the first match
            if (event.key === 'Enter' && devices.length > 0) {
              open(devices[0].nodeId)
            }
          }}
        />

        <label className="sr-only" htmlFor="finder-category">
          Category
        </label>
        <select
          id="finder-category"
          className="input"
          value={category}
          onChange={(event) => setCategory(event.target.value as SensorCategory | '')}
        >
          {categories.map((option) => (
            <option key={option.label} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="chips" role="group" aria-label="Filter by alert state">
        <button type="button" className="chip" aria-pressed={alertState === ''} onClick={() => setAlertState('')}>
          Any state
        </button>
        {states.map((state) => (
          <button
            key={state}
            type="button"
            className={`chip chip--${state.toLowerCase()}`}
            aria-pressed={alertState === state}
            onClick={() => setAlertState(alertState === state ? '' : state)}
          >
            {stateLabels[state]}
            <span className="chip__count mono">{counts[state]}</span>
          </button>
        ))}
      </div>

      {error && <p className="panel__empty panel__empty--error">{error}</p>}

      {!error && !loading && devices.length === 0 && (
        <p className="panel__empty">No devices match{filtered ? ' those filters' : ''}.</p>
      )}

      {devices.length > 0 && (
        <>
          <p className="finder__count" aria-live="polite">
            {devices.length} device{devices.length === 1 ? '' : 's'}
          </p>
          <ul className="results">
            {ordered.map((device) => {
              const value = device.category === 'Actuator' ? device.isOn : device.lastValue
              const flag = flagged.get(device.nodeId)

              return (
                <li key={device.nodeId}>
                  <button
                    type="button"
                    className={`result ${selected === device.nodeId ? 'result--selected' : ''} ${flag ? 'result--flagged' : ''}`}
                    onClick={() => open(device.nodeId)}
                    title={flag ? `Flagged: ${flag.title}` : undefined}
                  >
                    <span className={`result__dot result__dot--${device.alertState.toLowerCase()}`} aria-hidden="true" />
                    <span className="result__main">
                      <span className="result__id mono">
                        {device.nodeId}
                        {flag && <span className="flag">Flagged</span>}
                      </span>
                      <span className="result__meta">
                        {categoryLabel(device.category, device.nodeId)} · {device.location.split('/').slice(1).join(' / ')}
                      </span>
                    </span>
                    <span className="result__value mono">{formatValue(value, device.category, device.nodeId)}</span>
                    <span className="result__state">{stateLabels[device.alertState]}</span>
                  </button>
                </li>
              )
            })}
          </ul>
        </>
      )}
    </section>
  )
}
