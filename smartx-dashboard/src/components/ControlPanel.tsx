import { useState } from 'react'
import { ApiError, api } from '../api/client'
import type { Device, IssueCommand } from '../api/types'
import { useToast } from '../context/toastContext'
import { commandFor, commandLabels, effectiveState, isPending } from '../lib/commands'
import { categoryLabel, formatValue } from '../lib/format'

interface Props {
  devices: Device[]
  loading: boolean
  error: string | null
  markCommanded: (nodeIds: string[], state: boolean) => void
  refresh: () => void
}

// Manual overrides grouped by sub-zone, since that is how an operator thinks about the farm
export function ControlPanel({ devices, loading, error, markCommanded, refresh }: Props) {
  const toast = useToast()
  const [busy, setBusy] = useState<Set<string>>(new Set())
  const [confirmShut, setConfirmShut] = useState(false)

  const valves = devices.filter((device) => device.actuator === 'Valve')
  const openValves = valves.filter(effectiveState).length

  const zones = new Map<string, Device[]>()
  devices
    .slice()
    .sort((a, b) => a.location.localeCompare(b.location) || a.nodeId.localeCompare(b.nodeId))
    .forEach((device) => zones.set(device.location, [...(zones.get(device.location) ?? []), device]))

  const send = async (command: IssueCommand, targets: string[], state: boolean) => {
    setBusy((previous) => new Set([...previous, ...targets]))
    markCommanded(targets, state)

    try {
      const record = await api.issueCommand(command)
      const who = record.nodeIds.length === 1 ? record.nodeIds[0] : `${record.nodeIds.length} valves`
      toast(`${commandLabels[record.type]} sent to ${who}`, 'success')
    } catch (problem) {
      // Put the real state back since the optimistic one was wrong
      refresh()
      toast(problem instanceof ApiError ? problem.message : 'The gateway did not accept that command', 'error')
    } finally {
      setBusy((previous) => new Set([...previous].filter((id) => !targets.includes(id))))
    }
  }

  const toggle = (device: Device) => {
    const turnOn = !effectiveState(device)
    send({ nodeId: device.nodeId, type: commandFor(device.actuator, turnOn) }, [device.nodeId], turnOn)
  }

  const shutAll = () => {
    setConfirmShut(false)
    send({ type: 'ShutAllValves' }, valves.map((valve) => valve.nodeId), false)
  }

  return (
    <section className="panel">
      <header className="panel__head panel__head--wrap">
        <h2 className="panel__title">Manual overrides</h2>

        {/* Two clicks on purpose, shutting every valve on the farm should never be an accident */}
        {confirmShut ? (
          <div className="confirm" role="group" aria-label="Confirm shutting all valves">
            <span>Shut {openValves} open valve{openValves === 1 ? '' : 's'}?</span>
            <button type="button" className="button button--danger" onClick={shutAll}>
              Shut all
            </button>
            <button type="button" className="button" onClick={() => setConfirmShut(false)}>
              Cancel
            </button>
          </div>
        ) : (
          <button
            type="button"
            className="button button--danger-outline"
            onClick={() => setConfirmShut(true)}
            disabled={openValves === 0}
          >
            Shut all valves
          </button>
        )}
      </header>

      {loading && <p className="panel__empty">Loading actuators…</p>}
      {error && <p className="panel__empty panel__empty--error">{error}</p>}
      {!loading && !error && devices.length === 0 && (
        <p className="panel__empty">No valves, pumps or relays registered yet.</p>
      )}

      <div className="zones">
        {[...zones.entries()].map(([location, members]) => (
          <div key={location} className="zone-card">
            <h3 className="zone-card__title">{location.split('/').slice(1).join(' / ') || location}</h3>

            <ul className="controls">
              {members.map((device) => {
                const on = effectiveState(device)
                const pending = isPending(device)
                const offline = device.liveness === 'Stale'

                return (
                  <li key={device.nodeId} className={`control ${offline ? 'control--offline' : ''}`}>
                    <span className="control__info">
                      <span className="control__id mono">{device.nodeId}</span>
                      <span className="control__state">
                        {categoryLabel(device.category, device.nodeId)} · {formatValue(device.isOn, device.category, device.nodeId)}
                        {pending && <span className="control__pending"> → {formatValue(device.commandedState, device.category, device.nodeId)}</span>}
                        {offline && <span className="control__offline"> · offline, will apply when it is back</span>}
                      </span>
                    </span>

                    <button
                      type="button"
                      className={`switch ${on ? 'switch--on' : ''}`}
                      role="switch"
                      aria-checked={on}
                      aria-label={`${commandLabels[commandFor(device.actuator, !on)]} on ${device.nodeId}`}
                      disabled={busy.has(device.nodeId)}
                      onClick={() => toggle(device)}
                    >
                      <span className="switch__thumb" />
                    </button>
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </div>
    </section>
  )
}
