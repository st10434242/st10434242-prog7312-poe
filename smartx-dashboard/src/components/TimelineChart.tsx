import { useEffect, useRef, useState, type PointerEvent } from 'react'
import type { SensorCategory, TimelinePoint } from '../api/types'
import { formatValue } from '../lib/format'
import './TimelineChart.css'

const HEIGHT = 240
const PAD = { top: 12, right: 16, bottom: 26, left: 48 }

interface Threshold {
  value: number
  label: string
  level: 'caution' | 'alert'
}

// Same lines the gateway's alert classifier uses, so the graph shows exactly where an alert fires
const thresholds: Partial<Record<SensorCategory, Threshold[]>> = {
  Environmental: [
    { value: 90, label: 'Flooding 90%', level: 'alert' },
    { value: 80, label: 'High 80%', level: 'caution' },
    { value: 30, label: 'Dry 30%', level: 'caution' },
    { value: 20, label: 'Critical 20%', level: 'alert' },
  ],
  PowerConsumption: [
    { value: 1200, label: 'Critical 1200 W', level: 'alert' },
    { value: 1000, label: 'High 1000 W', level: 'caution' },
  ],
}

interface Props {
  points: TimelinePoint[]
  category: SensorCategory
  nodeId: string
}

export function TimelineChart({ points, category, nodeId }: Props) {
  const box = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(600)
  const [hover, setHover] = useState<number | null>(null)

  // Drawn in real pixels rather than a stretched viewBox so the text never gets squashed
  useEffect(() => {
    const element = box.current

    if (!element) {
      return
    }

    const observer = new ResizeObserver(([entry]) => setWidth(Math.max(280, entry.contentRect.width)))
    observer.observe(element)

    return () => observer.disconnect()
  }, [])

  if (points.length < 2) {
    return (
      <div ref={box} className="chart chart--empty">
        Waiting for readings from {nodeId}…
      </div>
    )
  }

  const times = points.map((point) => Date.parse(point.timestampUtc))
  const t0 = times[0]
  const t1 = Math.max(times[times.length - 1], t0 + 1000)
  const { lo, hi } = yRange(points, category)

  const plotW = width - PAD.left - PAD.right
  const plotH = HEIGHT - PAD.top - PAD.bottom
  const x = (time: number) => PAD.left + ((time - t0) / (t1 - t0)) * plotW
  const y = (value: number) => PAD.top + (1 - (value - lo) / (hi - lo)) * plotH

  // Actuators jump between states so they are drawn as steps, not slopes
  const path = points
    .map((point, index) => {
      const px = x(times[index]).toFixed(1)
      const py = y(point.value).toFixed(1)

      if (index === 0) {
        return `M${px},${py}`
      }

      return category === 'Actuator'
        ? `H${px}V${py}`
        : `L${px},${py}`
    })
    .join('')

  const yTicks = category === 'Actuator' ? [0, 1] : niceTicks(lo, hi)
  const xTicks = [0, 1 / 3, 2 / 3, 1].map((share) => t0 + share * (t1 - t0))

  // Under 10 minutes every tick would read the same minute, so show seconds as well
  const showSeconds = t1 - t0 < 10 * 60_000

  const onMove = (event: PointerEvent<SVGRectElement>) => {
    const bounds = event.currentTarget.getBoundingClientRect()
    const time = t0 + ((event.clientX - bounds.left) / bounds.width) * (t1 - t0)
    setHover(nearest(times, time))
  }

  const active = hover === null ? null : points[hover]
  const latest = points[points.length - 1]

  return (
    <div ref={box} className="chart">
      <svg
        width={width}
        height={HEIGHT}
        role="img"
        aria-label={`${nodeId} over the last ${points.length} readings, latest ${formatValue(latest.value, category, nodeId)}`}
      >
        {yTicks.map((tick) => (
          <g key={tick}>
            <line className="chart__grid" x1={PAD.left} x2={width - PAD.right} y1={y(tick)} y2={y(tick)} />
            <text className="chart__axis" x={PAD.left - 8} y={y(tick)} textAnchor="end" dominantBaseline="middle">
              {category === 'Actuator' ? (tick ? 'On' : 'Off') : tick}
            </text>
          </g>
        ))}

        {(thresholds[category] ?? [])
          .filter((line) => line.value > lo && line.value < hi)
          .map((line) => (
            <g key={line.value}>
              <line className={`chart__threshold chart__threshold--${line.level}`} x1={PAD.left} x2={width - PAD.right} y1={y(line.value)} y2={y(line.value)} />
              <text className="chart__threshold-label" x={PAD.left + 4} y={y(line.value) - 4}>
                {line.label}
              </text>
            </g>
          ))}

        {xTicks.map((tick, index) => (
          <text
            key={tick}
            className="chart__axis"
            x={x(tick)}
            y={HEIGHT - 6}
            textAnchor={index === 0 ? 'start' : index === xTicks.length - 1 ? 'end' : 'middle'}
          >
            {clock(tick, showSeconds)}
          </text>
        ))}

        <path className="chart__line" d={path} />

        {active && hover !== null && (
          <g>
            <line className="chart__crosshair" x1={x(times[hover])} x2={x(times[hover])} y1={PAD.top} y2={PAD.top + plotH} />
            <circle className="chart__dot" cx={x(times[hover])} cy={y(active.value)} r={4} />
          </g>
        )}

        {/* Wider than the plot line itself so the hover is easy to hit */}
        <rect
          className="chart__hit"
          x={PAD.left}
          y={PAD.top}
          width={plotW}
          height={plotH}
          onPointerMove={onMove}
          onPointerLeave={() => setHover(null)}
        />
      </svg>

      {active && hover !== null && (
        <div
          className="chart__tooltip"
          style={{
            left: Math.min(Math.max(x(times[hover]), 70), width - 70),
            top: Math.max(y(active.value) - 54, 0),
          }}
        >
          <strong className="mono">{formatValue(category === 'Actuator' ? active.value === 1 : active.value, category, nodeId)}</strong>
          <span>{clock(times[hover], true)}</span>
        </div>
      )}
    </div>
  )
}

function yRange(points: TimelinePoint[], category: SensorCategory) {
  if (category === 'Actuator') {
    return { lo: -0.2, hi: 1.2 }
  }

  // Moisture is a percentage so the full scale keeps the threshold lines in view
  if (category === 'Environmental') {
    return { lo: 0, hi: 100 }
  }

  const max = Math.max(...points.map((point) => point.value))
  return { lo: 0, hi: Math.max(1400, max * 1.1) }
}

function niceTicks(lo: number, hi: number): number[] {
  const rough = (hi - lo) / 4
  const magnitude = 10 ** Math.floor(Math.log10(rough))
  const step = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((s) => s >= rough) ?? rough
  const ticks: number[] = []

  for (let tick = Math.ceil(lo / step) * step; tick <= hi; tick += step) {
    ticks.push(Math.round(tick))
  }

  return ticks
}

// Times are sorted so a binary search finds the closest one quickly even with 500 points
function nearest(times: number[], target: number): number {
  let low = 0
  let high = times.length - 1

  while (low < high) {
    const mid = Math.floor((low + high) / 2)

    if (times[mid] < target) {
      low = mid + 1
    } else {
      high = mid
    }
  }

  if (low > 0 && target - times[low - 1] < times[low] - target) {
    return low - 1
  }

  return low
}

function clock(time: number, seconds = false): string {
  return new Date(time).toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    second: seconds ? '2-digit' : undefined,
  })
}
