import { useState } from 'react'
import { ApiError, api } from '../api/client'
import type { Insight, InsightSource, Pattern } from '../api/types'
import { useToast } from '../context/toastContext'
import { commandLabels } from '../lib/commands'

const sourceLabels: Record<InsightSource, string> = {
  Rule: 'Rule',
  Learned: 'Learned',
  Combined: 'Rule + learned',
}

interface Props {
  insights: Insight[]
  onRemove: (id: string) => void
  onShow: (nodeId: string) => void
}

// The engine's ranked suggestions, worst problem first, so the fix is one click away before anyone goes looking
export function InsightsPanel({ insights, onRemove, onShow }: Props) {
  const toast = useToast()
  const [busy, setBusy] = useState<string | null>(null)
  const [patterns, setPatterns] = useState<Pattern[] | null>(null)

  const apply = async (insight: Insight) => {
    if (!insight.command) {
      return
    }

    setBusy(insight.id)

    try {
      await api.issueCommand(insight.command)
      onRemove(insight.id)
      toast(`${commandLabels[insight.command.type]} sent to ${insight.command.nodeId ?? 'every valve'}`, 'success')
    } catch (problem) {
      toast(problem instanceof ApiError ? problem.message : 'Could not apply that suggestion', 'error')
    } finally {
      setBusy(null)
    }
  }

  const dismiss = async (insight: Insight) => {
    onRemove(insight.id)

    try {
      await api.dismissInsight(insight.id)
      toast(`Hid "${insight.title}" for 10 minutes`, 'info')
    } catch {
      toast('Could not dismiss that suggestion', 'error')
    }
  }

  // Only fetched when someone opens it, nobody needs the raw graph otherwise
  const loadPatterns = (open: boolean) => {
    if (open) {
      api
        .getPatterns()
        .then(setPatterns)
        .catch(() => setPatterns([]))
    }
  }

  return (
    <section className="panel insights" aria-labelledby="insights-title">
      <header className="panel__head">
        <h2 className="panel__title" id="insights-title">
          Suggested actions
        </h2>
        <span className="panel__hint">ranked by urgency</span>
      </header>

      {insights.length === 0 ? (
        <p className="panel__empty">Nothing needs attention right now.</p>
      ) : (
        <ol className="insight-list">
          {insights.map((insight) => (
            <li key={insight.id} className={`insight insight--${(insight.severity ?? 'Info').toLowerCase()}`}>
              <div className="insight__body">
                <div className="insight__top">
                  <span className="insight__title">{insight.title}</span>
                  <span className={`insight__source insight__source--${insight.source.toLowerCase()}`}>
                    {sourceLabels[insight.source]}
                  </span>
                  <span className="insight__score mono" title="Urgency score out of 100">
                    {insight.score}
                  </span>
                </div>
                <p className="insight__reason">{insight.reason}</p>
              </div>

              <div className="insight__actions">
                {insight.command && (
                  <button
                    type="button"
                    className="button button--primary"
                    disabled={busy === insight.id}
                    onClick={() => apply(insight)}
                  >
                    Apply
                  </button>
                )}
                <button type="button" className="button" onClick={() => onShow(insight.nodeId === 'ALL' ? insight.triggerNodeId ?? '' : insight.nodeId)}>
                  Show
                </button>
                <button type="button" className="button button--ghost" onClick={() => dismiss(insight)} aria-label={`Dismiss ${insight.title}`}>
                  Dismiss
                </button>
              </div>
            </li>
          ))}
        </ol>
      )}

      <details className="patterns" onToggle={(event) => loadPatterns(event.currentTarget.open)}>
        <summary>What the engine has learned</summary>

        {patterns === null ? (
          <p className="panel__empty">Loading…</p>
        ) : patterns.length === 0 ? (
          <p className="panel__empty">No patterns yet, they build up as you search and send commands.</p>
        ) : (
          <table className="patterns__table">
            <thead>
              <tr>
                <th scope="col">After</th>
                <th scope="col">You usually</th>
                <th scope="col">Seen</th>
                <th scope="col">Confidence</th>
              </tr>
            </thead>
            <tbody>
              {patterns.map((pattern) => (
                <tr key={`${pattern.from}-${pattern.to}`}>
                  <td className="mono">{readable(pattern.from)}</td>
                  <td className="mono">{readable(pattern.to)}</td>
                  <td className="mono">{pattern.count}×</td>
                  <td className="mono">{Math.round(pattern.confidence * 100)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </details>
    </section>
  )
}

// Keys look like command:StartPump:PUMP-Z1S1-01, this turns them into something you can read
function readable(key: string): string {
  const [kind, first, second] = key.split(':')

  switch (kind) {
    case 'alert':
      return `${first} on ${second}`
    case 'command':
      return `${first} ${second === 'ALL' ? 'everywhere' : second}`
    case 'search':
      return `look at ${first}`
    default:
      return key
  }
}
