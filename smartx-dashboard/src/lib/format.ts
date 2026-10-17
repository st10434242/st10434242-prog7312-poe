import type { ActuatorKind, SensorCategory, SensorValue } from '../api/types'

// Same naming rule the gateway uses to tell actuators apart
export function actuatorKindFor(nodeId: string): ActuatorKind {
  const id = nodeId.toUpperCase()

  if (id.startsWith('VALVE')) {
    return 'Valve'
  }

  if (id.startsWith('PUMP')) {
    return 'Pump'
  }

  if (id.startsWith('RELAY')) {
    return 'Relay'
  }

  return 'Generic'
}

const onOffLabels: Record<ActuatorKind, [string, string]> = {
  Valve: ['Open', 'Closed'],
  Pump: ['Running', 'Stopped'],
  Relay: ['On', 'Off'],
  Generic: ['On', 'Off'],
}

// Units come from the category, since the wire only carries a bare number
export function formatValue(value: SensorValue | null, category: SensorCategory, nodeId = ''): string {
  if (value === null || value === undefined) {
    return '—'
  }

  switch (category) {
    case 'Environmental':
      return `${Number(value).toFixed(1)}%`
    case 'PowerConsumption':
      return `${Number(value).toLocaleString()} W`
    case 'Actuator': {
      const [on, off] = onOffLabels[actuatorKindFor(nodeId)]
      return value ? on : off
    }
    default:
      return String(value)
  }
}

export function categoryLabel(category: SensorCategory, nodeId = ''): string {
  switch (category) {
    case 'Environmental':
      return 'Soil moisture'
    case 'PowerConsumption':
      return 'Power draw'
    case 'Actuator': {
      const kind = actuatorKindFor(nodeId)
      return kind === 'Generic' ? 'Switch' : kind
    }
    default:
      return category
  }
}

// Local wall clock time, seconds plus milliseconds for the packet feed where several land in the same second
export function clockTime(iso: string, precise = false): string {
  const date = new Date(iso)
  const time = date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })

  return precise ? `${time}.${String(date.getMilliseconds()).padStart(3, '0')}` : time
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`
  }

  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
