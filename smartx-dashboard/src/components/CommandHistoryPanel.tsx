import { useEffect, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { CommandHistory } from '../api/types'
import { useToast } from '../context/toastContext'
import { commandLabels, describeTargets } from '../lib/commands'
import { clockTime } from '../lib/format'

interface Props {
  history: CommandHistory | null
  refresh: () => void
}

// The undo stack, newest on top, undo always takes the top one off
export function CommandHistoryPanel({ history, refresh }: Props) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)

  const next = history?.nextUndo ?? null
  const canUndo = !busy && (history?.undoDepth ?? 0) > 0

  const undo = async () => {
    if (!canUndo) {
      return
    }

    setBusy(true)

    try {
      const record = await api.undoCommand()
      toast(`Undid ${commandLabels[record.type].toLowerCase()} on ${describeTargets(record)}`, 'success')
    } catch (problem) {
      toast(problem instanceof ApiError ? problem.message : 'Undo failed', 'error')
    } finally {
      setBusy(false)
      refresh()
    }
  }

  // Ctrl+Z like any other app, but not while typing in a box where it should undo the text
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const typing = event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement || event.target instanceof HTMLSelectElement

      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z' && !event.shiftKey && !typing) {
        event.preventDefault()
        void undo()
      }
    }

    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  })

  return (
    <section className="panel">
      <header className="panel__head">
        <h2 className="panel__title">Command history</h2>
        <span className="panel__hint">{history?.undoDepth ?? 0} on the undo stack</span>
      </header>

      <button type="button" className="button button--primary undo" onClick={undo} disabled={!canUndo}>
        <span>{next ? `Undo ${commandLabels[next.type].toLowerCase()}` : 'Nothing to undo'}</span>
        {next && <span className="undo__target mono">{describeTargets(next)}</span>}
        <kbd className="undo__key">Ctrl Z</kbd>
      </button>

      {history && history.commands.length > 0 ? (
        <ol className="history">
          {history.commands.map((record) => (
            <li key={record.id} className={`history__row ${record.reverted ? 'history__row--reverted' : ''}`}>
              <span className="history__what">
                {commandLabels[record.type]}
                {record.reverted && <span className="history__tag">undone</span>}
              </span>
              <span className="history__target mono">{describeTargets(record)}</span>
              <span className="history__time mono">{clockTime(record.issuedAtUtc)}</span>
            </li>
          ))}
        </ol>
      ) : (
        <p className="panel__empty">No commands sent yet.</p>
      )}
    </section>
  )
}
