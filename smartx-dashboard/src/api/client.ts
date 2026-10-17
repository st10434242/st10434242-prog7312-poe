import type {
  Activity,
  Alert,
  Attachment,
  CommandHistory,
  CommandRecord,
  Device,
  DeviceFilter,
  Faults,
  Insight,
  IssueCommand,
  NodeHistory,
  NodeTimeline,
  Pattern,
  QueueStats,
  Sensor,
  SensorRegistration,
  TimelineEvent,
  Topology,
  ValidationProblem,
} from './types'

// Thrown for any non-2xx so callers can show the gateway's own field messages
export class ApiError extends Error {
  status: number
  fieldErrors: Record<string, string[]>

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init)

  if (!response.ok) {
    throw await toApiError(response)
  }

  // 204 and friends have nothing to parse
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ValidationProblem = {}

  try {
    problem = (await response.json()) as ValidationProblem
  } catch {
    // Not every failure has a JSON body
  }

  const message =
    problem.detail ??
    (problem.errors ? Object.values(problem.errors).flat().join(' ') : '') ??
    problem.title ??
    `Request failed with ${response.status}`

  return new ApiError(message || `Request failed with ${response.status}`, response.status, problem.errors ?? {})
}

export const api = {
  getSensors: () => request<Sensor[]>('/api/sensors'),

  getSensor: (nodeId: string) => request<Sensor>(`/api/sensors/${encodeURIComponent(nodeId)}`),

  registerSensor: (registration: SensorRegistration) =>
    request<Sensor>('/api/sensors', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(registration),
    }),

  deleteSensor: (nodeId: string) =>
    request<void>(`/api/sensors/${encodeURIComponent(nodeId)}`, { method: 'DELETE' }),

  getHistory: (nodeId: string) =>
    request<NodeHistory>(`/api/telemetry/${encodeURIComponent(nodeId)}`),

  getTopology: () => request<Topology>('/api/topology'),

  getAttachments: (nodeId: string) =>
    request<Attachment[]>(`/api/sensors/${encodeURIComponent(nodeId)}/attachments`),

  uploadAttachment: (nodeId: string, file: File) => {
    const form = new FormData()
    form.append('file', file)

    return request<Attachment>(`/api/sensors/${encodeURIComponent(nodeId)}/attachments`, {
      method: 'POST',
      body: form,
    })
  },

  deleteAttachment: (id: string) => request<void>(`/api/attachments/${id}`, { method: 'DELETE' }),

  downloadAttachmentUrl: (id: string) => `/api/attachments/${id}`,

  getSilenced: () => request<string[]>('/api/debug/silence'),

  silenceNode: (nodeId: string) =>
    request<void>(`/api/debug/silence/${encodeURIComponent(nodeId)}`, { method: 'POST' }),

  restoreNode: (nodeId: string) =>
    request<void>(`/api/debug/silence/${encodeURIComponent(nodeId)}`, { method: 'DELETE' }),

  getStats: () => request<QueueStats>('/api/stream/stats'),

  getAlerts: (limit = 50) => request<Alert[]>(`/api/stream/alerts?limit=${limit}`),

  getFaults: () => request<Faults>('/api/stream/faults'),

  getEvents: (limit = 60) => request<TimelineEvent[]>(`/api/stream/events?limit=${limit}`),

  getTimeline: (nodeId: string, from?: Date, limit = 300) => {
    const query = new URLSearchParams({ limit: String(limit) })

    if (from) {
      query.set('from', from.toISOString())
    }

    return request<NodeTimeline>(`/api/stream/timeline/${encodeURIComponent(nodeId)}?${query}`)
  },

  getDevices: (filter: DeviceFilter = {}) => {
    const query = new URLSearchParams()

    // Only send what is set, the gateway treats a missing filter as match everything
    Object.entries(filter).forEach(([key, value]) => {
      if (value) {
        query.set(key, value)
      }
    })

    return request<Device[]>(`/api/devices?${query}`)
  },

  issueCommand: (command: IssueCommand) =>
    request<CommandRecord>('/api/commands', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(command),
    }),

  undoCommand: () => request<CommandRecord>('/api/commands/undo', { method: 'POST' }),

  getCommandHistory: () => request<CommandHistory>('/api/commands/history'),

  getInsights: () => request<Insight[]>('/api/insights'),

  recordActivity: (activity: Activity) =>
    request<void>('/api/insights/activity', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(activity),
    }),

  dismissInsight: (id: string) =>
    request<void>(`/api/insights/${encodeURIComponent(id)}/dismiss`, { method: 'POST' }),

  getPatterns: () => request<Pattern[]>('/api/insights/patterns'),
}
