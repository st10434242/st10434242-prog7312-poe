import { useState } from 'react'
import { ApiError, api } from '../api/client'
import type { SensorCategory, SensorRegistration } from '../api/types'
import { validateRegistration } from '../lib/validation'
import './RegisterSensorForm.css'

const empty: SensorRegistration = {
  nodeId: '',
  mac: '',
  location: '',
  category: 'Environmental',
  expectedIntervalMs: 1000,
}

const categories: { value: SensorCategory; label: string }[] = [
  { value: 'Environmental', label: 'Environmental (soil moisture, float)' },
  { value: 'PowerConsumption', label: 'Power consumption (watts, int)' },
  { value: 'Actuator', label: 'Actuator (valve state, bool)' },
]

export function RegisterSensorForm({ onRegistered }: { onRegistered: () => void }) {
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState<SensorRegistration>(empty)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [result, setResult] = useState<{ ok: boolean; message: string } | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const set = <K extends keyof SensorRegistration>(key: K, value: SensorRegistration[K]) => {
    setForm((previous) => ({ ...previous, [key]: value }))
    setErrors((previous) => ({ ...previous, [key]: '' }))
    setResult(null)
  }

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()

    const found = validateRegistration(form)
    setErrors(found)

    if (Object.keys(found).length > 0) {
      return
    }

    setSubmitting(true)

    try {
      await api.registerSensor({ ...form, nodeId: form.nodeId.trim(), mac: form.mac.trim() })
      setResult({ ok: true, message: `${form.nodeId.trim()} registered.` })
      setForm(empty)
      onRegistered()
    } catch (problem) {
      if (problem instanceof ApiError) {
        // The gateway's own field messages, lower-cased to match the form's keys
        setErrors(
          Object.fromEntries(
            Object.entries(problem.fieldErrors).map(([field, messages]) => [
              field.charAt(0).toLowerCase() + field.slice(1),
              messages.join(' '),
            ]),
          ),
        )
        setResult({ ok: false, message: problem.message })
      } else {
        setResult({ ok: false, message: 'Could not reach the gateway.' })
      }
    } finally {
      setSubmitting(false)
    }
  }

  if (!open) {
    return (
      <div className="register">
        <button type="button" className="register__toggle" onClick={() => setOpen(true)}>
          Register a sensor
        </button>
      </div>
    )
  }

  return (
    <div className="register">
      <button type="button" className="register__toggle" onClick={() => setOpen(false)}>
        Hide registration
      </button>

      <form className="register__panel" onSubmit={submit} noValidate>
        <div className="register__grid">
          <Field label="Node ID" error={errors.nodeId} hint="e.g. ESP32-7F2A">
            <input value={form.nodeId} onChange={(e) => set('nodeId', e.target.value)} />
          </Field>

          <Field label="MAC address" error={errors.mac} hint="AA:BB:CC:DD:EE:FF">
            <input value={form.mac} onChange={(e) => set('mac', e.target.value)} />
          </Field>

          <Field label="Deployment location" error={errors.location} hint="FacilityA/Zone1/SubZoneB">
            <input value={form.location} onChange={(e) => set('location', e.target.value)} />
          </Field>

          <Field label="Category" error={errors.category}>
            <select
              value={form.category}
              onChange={(e) => set('category', e.target.value as SensorCategory)}
            >
              {categories.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </Field>

          <Field
            label="Publishes every"
            error={errors.expectedIntervalMs}
            hint="milliseconds, drives staleness"
          >
            <input
              type="number"
              value={form.expectedIntervalMs}
              onChange={(e) => set('expectedIntervalMs', Number(e.target.value))}
            />
          </Field>
        </div>

        <div className="register__actions">
          <button type="submit" className="register__submit" disabled={submitting}>
            {submitting ? 'Registering…' : 'Register'}
          </button>

          {result && (
            <span
              className={`register__result register__result--${result.ok ? 'ok' : 'bad'}`}
              role="status"
            >
              {result.message}
            </span>
          )}
        </div>
      </form>
    </div>
  )
}

function Field({
  label,
  error,
  hint,
  children,
}: {
  label: string
  error?: string
  hint?: string
  children: React.ReactNode
}) {
  return (
    <label className={`field ${error ? 'field--invalid' : ''}`}>
      <span className="field__label">{label}</span>
      {children}
      {/* The hint disappears once there is something more useful to say */}
      {error ? <span className="field__error">{error}</span> : <span className="field__hint">{hint}</span>}
    </label>
  )
}
