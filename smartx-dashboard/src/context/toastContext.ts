import { createContext, useContext } from 'react'

export type ToastTone = 'success' | 'error' | 'info'

export interface Toast {
  id: number
  tone: ToastTone
  message: string
}

export const ToastContext = createContext<((message: string, tone?: ToastTone) => void) | null>(null)

// Returns a function that pops a short message in the corner
export function useToast() {
  const show = useContext(ToastContext)

  if (!show) {
    throw new Error('useToast has to be used inside ToastProvider')
  }

  return show
}
