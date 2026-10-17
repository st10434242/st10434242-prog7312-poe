import { useCallback, useRef, useState, type ReactNode } from 'react'
import { ToastContext, type Toast, type ToastTone } from './toastContext'
import './Toasts.css'

const SHOW_FOR_MS = 4000
const MAX_TOASTS = 4

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const nextId = useRef(0)

  const dismiss = useCallback((id: number) => {
    setToasts((previous) => previous.filter((toast) => toast.id !== id))
  }, [])

  const show = useCallback(
    (message: string, tone: ToastTone = 'info') => {
      nextId.current += 1
      const id = nextId.current

      setToasts((previous) => [...previous, { id, tone, message }].slice(-MAX_TOASTS))
      setTimeout(() => dismiss(id), SHOW_FOR_MS)
    },
    [dismiss],
  )

  return (
    <ToastContext.Provider value={show}>
      {children}

      {/* Polite so a screen reader hears the result without being cut off mid sentence */}
      <div className="toasts" role="status" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast--${toast.tone}`}>
            <span className="toast__icon" aria-hidden="true">
              {toast.tone === 'success' ? '✓' : toast.tone === 'error' ? '!' : 'i'}
            </span>
            <span className="toast__message">{toast.message}</span>
            <button type="button" className="toast__close" onClick={() => dismiss(toast.id)} aria-label="Dismiss">
              ×
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
