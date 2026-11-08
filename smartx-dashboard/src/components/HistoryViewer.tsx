import { useEffect, useMemo, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { BenchmarkRow, HistoryCoverage, HistoryRange } from '../api/types'
import { useSessionState } from '../hooks/useSessionState'
import { formatValue } from '../lib/format'
import { TimelineChart } from './TimelineChart'
import './HistoryViewer.css'

type Preset = 'hour' | 'six' | 'day' | 'afternoon' | 'custom'

const presets: { id: Preset; label: string }[] = [
  { id: 'hour', label: 'Last hour' },
  { id: 'six', label: 'Last 6 h' },
  { id: 'day', label: 'Last 24 h' },
  { id: 'afternoon', label: '14:00 to 16:00' },
  { id: 'custom', label: 'Custom' },
]

const TABLE_ROWS = 200
const HOUR = 3_600_000

// The last 14:00 to 16:00 that has fully happened, today once it is past four, otherwise yesterday
function afternoonWindow(now: Date) {
  const from = new Date(now)
  from.setHours(14, 0, 0, 0)

  if (now.getHours() < 16) {
    from.setDate(from.getDate() - 1)
  }

  return { from, to: new Date(from.getTime() + (2 * HOUR)) }
}

function windowFor(preset: Preset, custom: { from: string; to: string }) {
  const now = new Date()

  switch (preset) {
    case 'hour':
      return { from: new Date(now.getTime() - HOUR), to: now }
    case 'six':
      return { from: new Date(now.getTime() - (6 * HOUR)), to: now }
    case 'day':
      return { from: new Date(now.getTime() - (24 * HOUR)), to: now }
    case 'afternoon':
      return afternoonWindow(now)
    case 'custom':
      return { from: new Date(custom.from), to: new Date(custom.to) }
  }
}

// datetime-local wants local time without a zone, toISOString would shift it to UTC
function toLocalInput(date: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

// The custom range starts on the last 14:00 to 16:00 so it has something sensible in it
const defaultCustom = (() => {
  const { from, to } = afternoonWindow(new Date())
  return { from: toLocalInput(from), to: toLocalInput(to) }
})()

function stamp(iso: string) {
  return new Date(iso).toLocaleString([], {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  })
}

interface Props {
  selected: string | null
  onSelect: (nodeId: string) => void
}

export function HistoryViewer({ selected, onSelect }: Props) {
  const [coverage, setCoverage] = useState<HistoryCoverage[]>([])
  const [preset, setPreset] = useSessionState<Preset>('topology.historyPreset', 'hour')
  const [custom, setCustom] = useSessionState('topology.historyCustom', defaultCustom)
  const [result, setResult] = useState<HistoryRange | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loadedKey, setLoadedKey] = useState('')
  const [reloadToken, setReloadToken] = useState(0)

  useEffect(() => {
    let cancelled = false

    api
      .getHistoryCoverage()
      .then((found) => !cancelled && setCoverage(found))
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [])

  const node = coverage.find((item) => item.nodeId.toLowerCase() === selected?.toLowerCase()) ?? null

  const customProblem =
    preset !== 'custom'
      ? null
      : Number.isNaN(Date.parse(custom.from)) || Number.isNaN(Date.parse(custom.to))
        ? 'Pick both a start and an end.'
        : Date.parse(custom.from) >= Date.parse(custom.to)
          ? 'The start has to be before the end.'
          : null

  const nodeId = node?.nodeId ?? null

  // Every request gets a key, it is loading until the answer for the current key comes back
  const requestKey = `${nodeId}|${preset}|${custom.from}|${custom.to}|${reloadToken}`
  const loading = !!nodeId && !customProblem && loadedKey !== requestKey

  useEffect(() => {
    if (!nodeId || customProblem) {
      return
    }

    let cancelled = false
    const { from, to } = windowFor(preset, custom)

    api
      .getHistoryRange(nodeId, from, to)
      .then((found) => {
        if (!cancelled) {
          setResult(found)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setResult(null)
          setError(err instanceof ApiError ? err.message : 'Could not load that history.')
        }
      })
      .finally(() => !cancelled && setLoadedKey(requestKey))

    return () => {
      cancelled = true
    }
  }, [nodeId, preset, custom, customProblem, requestKey])

  const shown = result && node && result.nodeId === node.nodeId ? result : null

  // Newest first in the table, that is usually what someone scrolling a log wants
  const rows = useMemo(() => (shown ? [...shown.points].reverse().slice(0, TABLE_ROWS) : []), [shown])

  const groups = useMemo(() => {
    const byCategory = new Map<string, HistoryCoverage[]>()
    coverage.forEach((item) => byCategory.set(item.category, [...(byCategory.get(item.category) ?? []), item]))
    return [...byCategory.entries()]
  }, [coverage])

  const value = (raw: number | null) => (raw === null || !node ? '—' : formatValue(raw, node.category, node.nodeId))
  const isActuator = node?.category === 'Actuator'

  return (
    <div className="hlog">
      <div className="hlog__controls">
        <label className="hlog__field">
          <span>Node</span>
          <select className="input" value={node?.nodeId ?? ''} onChange={(event) => onSelect(event.target.value)}>
            <option value="" disabled>
              Pick a sensor
            </option>
            {groups.map(([category, items]) => (
              <optgroup key={category} label={category}>
                {items.map((item) => (
                  <option key={item.nodeId} value={item.nodeId}>
                    {item.nodeId} ({item.readings.toLocaleString()})
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        </label>

        <div className="segmented" role="group" aria-label="Time range">
          {presets.map((item) => (
            <button
              key={item.id}
              type="button"
              className="segmented__item"
              aria-pressed={preset === item.id}
              onClick={() => setPreset(item.id)}
            >
              {item.label}
            </button>
          ))}
        </div>

        {preset === 'custom' && (
          <div className="hlog__custom">
            <label className="hlog__field">
              <span>From</span>
              <input
                type="datetime-local"
                className="input"
                value={custom.from}
                onChange={(event) => setCustom({ ...custom, from: event.target.value })}
              />
            </label>
            <label className="hlog__field">
              <span>To</span>
              <input
                type="datetime-local"
                className="input"
                value={custom.to}
                onChange={(event) => setCustom({ ...custom, to: event.target.value })}
              />
            </label>
          </div>
        )}

        <button type="button" className="panel__action" onClick={() => setReloadToken((token) => token + 1)} disabled={!node}>
          {loading ? 'Loading…' : 'Refresh'}
        </button>
      </div>

      {customProblem && <p className="panel__empty panel__empty--error">{customProblem}</p>}

      {!node && !customProblem && (
        <p className="panel__empty">
          {selected && selected !== 'GATEWAY'
            ? `${selected} has no history yet.`
            : 'Pick a sensor here or click one on the map to browse what it reported.'}
        </p>
      )}

      {node && error && <p className="panel__empty panel__empty--error">{error}</p>}

      {shown && !customProblem && (
        <>
          <dl className="hlog__stats">
            <div>
              <dt>Window</dt>
              <dd>
                {stamp(shown.fromUtc)} to {stamp(shown.toUtc)}
              </dd>
            </div>
            <div>
              <dt>Readings</dt>
              <dd className="mono">
                {shown.count.toLocaleString()}
                {shown.downsampled && <span className="hlog__muted"> ({shown.points.length.toLocaleString()} plotted)</span>}
              </dd>
            </div>
            <div>
              {/* On and off readings are 1 and 0, so the average is how much of the window it was on */}
              <dt>{isActuator ? 'On for' : 'Min / avg / max'}</dt>
              <dd className="mono">
                {isActuator
                  ? shown.average === null ? '—' : `${Math.round(shown.average * 100)}% of the window`
                  : `${value(shown.min)} / ${value(shown.average)} / ${value(shown.max)}`}
              </dd>
            </div>
            <div>
              <dt>AVL range query</dt>
              <dd className="mono">
                {shown.queryMicroseconds.toLocaleString()} µs, {shown.nodesVisited.toLocaleString()} of {shown.treeSize.toLocaleString()} nodes
                visited, height {shown.treeHeight}
              </dd>
            </div>
          </dl>

          {shown.count === 0 ? (
            <p className="panel__empty">Nothing was recorded in this window.</p>
          ) : (
            <div className="hlog__body">
              <TimelineChart points={shown.points} category={node!.category} nodeId={node!.nodeId} />

              <div className="hlog__table-wrap">
                <table className="hlog__table">
                  <thead>
                    <tr>
                      <th>Time</th>
                      <th>Value</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((point, index) => (
                      <tr key={`${point.timestampUtc}-${index}`}>
                        <td className="mono">{stamp(point.timestampUtc)}</td>
                        <td className="mono">
                          {node!.category === 'Actuator'
                            ? formatValue(point.value === 1, node!.category, node!.nodeId)
                            : formatValue(point.value, node!.category, node!.nodeId)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {shown.points.length > TABLE_ROWS && (
                  <p className="hlog__muted hlog__note">Newest {TABLE_ROWS} shown, the chart has the whole window.</p>
                )}
              </div>
            </div>
          )}
        </>
      )}

      <BenchmarkCard />
    </div>
  )
}

// Same range query against a plain list scan, to show where the tree starts paying off
function BenchmarkCard() {
  const [rows, setRows] = useState<BenchmarkRow[] | null>(null)
  const [running, setRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const run = async () => {
    setRunning(true)
    setError(null)

    try {
      setRows(await api.runBenchmark())
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'The benchmark did not finish.')
    } finally {
      setRunning(false)
    }
  }

  return (
    <section className="bench">
      <header className="bench__head">
        <div>
          <h3>AVL tree against a list scan</h3>
          <p className="hlog__muted">
            Same 10 minute window queried 50 times at each size, the list has to check every reading, the tree only walks down to the window.
          </p>
        </div>
        <button type="button" className="panel__action" onClick={run} disabled={running}>
          {running ? 'Running…' : rows ? 'Run again' : 'Run benchmark'}
        </button>
      </header>

      {error && <p className="panel__empty panel__empty--error">{error}</p>}

      {running && !rows && <p className="hlog__muted">Building trees up to a million readings, this takes a few seconds.</p>}

      {rows && (
        <div className="hlog__table-wrap hlog__table-wrap--auto">
          <table className="hlog__table bench__table">
            <thead>
              <tr>
                <th>Readings</th>
                <th>Height</th>
                <th>Results</th>
                <th>AVL</th>
                <th>Nodes visited</th>
                <th>List</th>
                <th>Items scanned</th>
                <th>Faster</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.size}>
                  <td className="mono">{row.size.toLocaleString()}</td>
                  <td className="mono">{row.treeHeight}</td>
                  <td className="mono">{row.averageResults.toLocaleString()}</td>
                  <td className="mono">{row.avlMicroseconds.toLocaleString()} µs</td>
                  <td className="mono">{row.avlNodesVisited.toLocaleString()}</td>
                  <td className="mono">{row.listMicroseconds.toLocaleString()} µs</td>
                  <td className="mono">{row.listItemsScanned.toLocaleString()}</td>
                  <td className={`mono ${row.speedup >= 1 ? 'bench__win' : 'bench__loss'}`}>{row.speedup}×</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
