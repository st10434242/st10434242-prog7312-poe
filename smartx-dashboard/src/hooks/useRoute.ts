import { useEffect, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { MeshRoute } from '../api/types'

// Retraces whenever the mesh moves, version is the snapshot time so a reroute shows up on its own
export function useRoute(nodeId: string | null, version: string | undefined) {
  const [route, setRoute] = useState<MeshRoute | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!nodeId) {
      return
    }

    let cancelled = false

    api
      .getRoute(nodeId)
      .then((found) => {
        if (!cancelled) {
          setRoute(found)
          setError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setRoute(null)
          setError(err instanceof ApiError ? err.message : 'Could not trace that node.')
        }
      })

    return () => {
      cancelled = true
    }
  }, [nodeId, version])

  // A route left over from the last pick shouldn't show against a new one
  const current = route && nodeId && route.nodeId.toLowerCase() === nodeId.toLowerCase() ? route : null

  return { route: current, error: nodeId ? error : null }
}
