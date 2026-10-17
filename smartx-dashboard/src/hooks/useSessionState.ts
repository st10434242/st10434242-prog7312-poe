import { useEffect, useState } from 'react'

// useState that survives leaving the page and coming back, storage can throw in private windows so it is all guarded
export function useSessionState<T>(key: string, initial: T) {
  const [value, setValue] = useState<T>(() => {
    try {
      const saved = sessionStorage.getItem(key)
      return saved === null ? initial : (JSON.parse(saved) as T)
    } catch {
      return initial
    }
  })

  useEffect(() => {
    try {
      sessionStorage.setItem(key, JSON.stringify(value))
    } catch {
      // Not being able to save just means the filter resets next time
    }
  }, [key, value])

  return [value, setValue] as const
}
