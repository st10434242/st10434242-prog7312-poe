import type { SensorCategory } from '../api/types'

const WIDTH = 100
const HEIGHT = 24

interface Props {
  values: number[]
  category: SensorCategory
}

export function Sparkline({ values, category }: Props) {
  if (values.length < 2) {
    return <div className="spark spark--empty" aria-hidden="true" />
  }

  const { lo, hi } = range(values, category)
  const step = WIDTH / (values.length - 1)

  const points = values
    .map((value, index) => {
      const y = HEIGHT - ((value - lo) / (hi - lo)) * HEIGHT
      return `${(index * step).toFixed(1)},${clamp(y, 0, HEIGHT).toFixed(1)}`
    })
    .join(' ')

  return (
    <svg
      className="spark"
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      preserveAspectRatio="none"
      aria-hidden="true"
    >
      <polyline points={points} />
    </svg>
  )
}

function range(values: number[], category: SensorCategory) {
  // A valve is only ever open or shut, so a fixed scale draws it as a clean step
  if (category === 'Actuator') {
    return { lo: -0.15, hi: 1.15 }
  }

  const min = Math.min(...values)
  const max = Math.max(...values)
  const mean = values.reduce((total, value) => total + value, 0) / values.length

  // Floor the span relative to the mean, otherwise ordinary jitter gets stretched to fill the
  // tile and every sensor looks like it is spiking. A real spike still dominates the scale.
  const span = Math.max(max - min, Math.abs(mean) * 0.3, 0.001)
  const middle = (min + max) / 2

  return { lo: middle - span / 2, hi: middle + span / 2 }
}

function clamp(value: number, low: number, high: number) {
  return Math.min(Math.max(value, low), high)
}
