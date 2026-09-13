import type { SensorCategory, SensorValue } from '../api/types'

// Units come from the category, since the wire only carries a bare number
export function formatValue(value: SensorValue | null, category: SensorCategory): string {
  if (value === null || value === undefined) {
    return '—'
  }

  switch (category) {
    case 'Environmental':
      return `${Number(value).toFixed(1)}%`
    case 'PowerConsumption':
      return `${Number(value).toLocaleString()} W`
    case 'Actuator':
      return value ? 'Open' : 'Closed'
    default:
      return String(value)
  }
}

export function categoryLabel(category: SensorCategory): string {
  switch (category) {
    case 'Environmental':
      return 'Soil moisture'
    case 'PowerConsumption':
      return 'Power draw'
    case 'Actuator':
      return 'Valve'
    default:
      return category
  }
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
