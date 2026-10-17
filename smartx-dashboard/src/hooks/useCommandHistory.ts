import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import type { CommandHistory } from '../api/types'
import { useHubEvent } from '../context/hubContext'

// The undo stack as the gateway sees it, refetched whenever a command goes out or gets undone
export function useCommandHistory() {
  const [history, setHistory] = useState<CommandHistory | null>(null)
  const [reloadToken, setReloadToken] = useState(0)

  const refresh = useCallback(() => setReloadToken((token) => token + 1), [])

  useEffect(() => {
    let cancelled = false

    api
      .getCommandHistory()
      .then((found) => !cancelled && setHistory(found))
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [reloadToken])

  useHubEvent('CommandIssued', refresh)
  useHubEvent('CommandReverted', refresh)

  return { history, refresh }
}
