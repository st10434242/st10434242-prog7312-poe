import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { ZoneSummary } from '../api/types'

export function useTopology(sensorCount: number) {
  const [root, setRoot] = useState<ZoneSummary | null>(null)

  useEffect(() => {
    let cancelled = false

    api
      .getTopology()
      .then((topology) => !cancelled && setRoot(topology.root))
      .catch(() => !cancelled && setRoot(null))

    return () => {
      cancelled = true
    }
    // Refetched when the fleet size changes, since that is when zones appear or disappear
  }, [sensorCount])

  return root
}

export function findZone(root: ZoneSummary | null, path: string[]): ZoneSummary | null {
  let current = root

  for (const segment of path) {
    current =
      current?.children.find(
        (child) => child.name.toLowerCase() === segment.toLowerCase(),
      ) ?? null

    if (!current) {
      return null
    }
  }

  return current
}
