import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { MeshSnapshot, SpanningTree } from '../api/types'
import { useHubEvent, useHubStatus } from '../context/hubContext'

export interface MeshState {
  snapshot: MeshSnapshot | null
  backbone: SpanningTree | null
  shortest: SpanningTree | null
  error: string | null
  loading: boolean
  refresh: () => void
}

// The live mesh plus both trees, the gateway only pushes MeshChanged when something actually moved
export function useMesh(): MeshState {
  const [snapshot, setSnapshot] = useState<MeshSnapshot | null>(null)
  const [backbone, setBackbone] = useState<SpanningTree | null>(null)
  const [shortest, setShortest] = useState<SpanningTree | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [reloadToken, setReloadToken] = useState(0)
  const status = useHubStatus()

  const refresh = useCallback(() => setReloadToken((token) => token + 1), [])

  useEffect(() => {
    let cancelled = false

    // All three come from the same moment so the map and the trees never disagree
    Promise.all([api.getMesh(), api.getMst('Prim'), api.getShortest()])
      .then(([mesh, mst, routes]) => {
        if (cancelled) {
          return
        }

        setSnapshot(mesh)
        setBackbone(mst)
        setShortest(routes)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setError(err instanceof ApiError ? err.message : 'Could not load the mesh.')
        }
      })
      .finally(() => !cancelled && setLoading(false))

    return () => {
      cancelled = true
    }
    // Reconnecting reloads as well, a MeshChanged could have been missed while the hub was down
  }, [reloadToken, status])

  useHubEvent('MeshChanged', refresh)

  return { snapshot, backbone, shortest, error, loading, refresh }
}
