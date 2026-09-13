import { useEffect, useRef, useState } from 'react'
import { ApiError, api } from '../api/client'
import type { Attachment } from '../api/types'
import { formatBytes } from '../lib/format'
import { ALLOWED_EXTENSIONS, validateAttachment } from '../lib/validation'
import './AttachmentsPanel.css'

export function AttachmentsPanel({ nodeId }: { nodeId: string }) {
  const [attachments, setAttachments] = useState<Attachment[]>([])
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)
  const [uploading, setUploading] = useState(false)
  const input = useRef<HTMLInputElement>(null)

  useEffect(() => {
    let cancelled = false

    api
      .getAttachments(nodeId)
      .then((found) => !cancelled && setAttachments(found))
      .catch(() => !cancelled && setAttachments([]))

    return () => {
      cancelled = true
    }
  }, [nodeId])

  const upload = async (file: File) => {
    const problem = validateAttachment(file)

    if (problem) {
      setMessage({ ok: false, text: problem })
      return
    }

    setUploading(true)
    setMessage(null)

    try {
      const saved = await api.uploadAttachment(nodeId, file)
      setAttachments((previous) => [...previous, saved])
      setMessage({ ok: true, text: `${saved.fileName} attached and encrypted.` })
    } catch (error) {
      setMessage({
        ok: false,
        text: error instanceof ApiError ? error.message : 'Upload failed.',
      })
    } finally {
      setUploading(false)

      if (input.current) {
        input.current.value = ''
      }
    }
  }

  const remove = async (attachment: Attachment) => {
    await api.deleteAttachment(attachment.id)
    setAttachments((previous) => previous.filter((item) => item.id !== attachment.id))
    setMessage({ ok: true, text: `${attachment.fileName} removed.` })
  }

  return (
    <section className="node__panel">
      <p className="node__panel-title">Attachments</p>

      {attachments.length === 0 ? (
        <p className="attach__empty">No configuration files, logs or photos attached yet.</p>
      ) : (
        <ul className="attach__list">
          {attachments.map((attachment) => (
            <li key={attachment.id} className="attach__item">
              <div className="attach__meta">
                {/* Downloads stream back through the gateway, which decrypts on the way out */}
                <a href={api.downloadAttachmentUrl(attachment.id)} className="attach__name">
                  {attachment.fileName}
                </a>
                <span className="attach__detail">
                  {formatBytes(attachment.sizeBytes)} ·{' '}
                  {new Date(attachment.uploadedAtUtc).toLocaleTimeString()}
                </span>
              </div>
              <button
                type="button"
                className="attach__remove"
                onClick={() => remove(attachment)}
                aria-label={`Remove ${attachment.fileName}`}
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="attach__upload">
        <input
          ref={input}
          type="file"
          accept={ALLOWED_EXTENSIONS.join(',')}
          disabled={uploading}
          onChange={(event) => {
            const file = event.target.files?.[0]

            if (file) {
              void upload(file)
            }
          }}
        />
        {uploading && <span className="attach__status">Encrypting and uploading…</span>}
      </div>

      {message && (
        <p className={`attach__message attach__message--${message.ok ? 'ok' : 'bad'}`} role="status">
          {message.text}
        </p>
      )}
    </section>
  )
}
