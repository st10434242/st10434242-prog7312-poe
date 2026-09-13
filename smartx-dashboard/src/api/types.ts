// Mirrors the contracts in SmartX.Core so the two ends agree on shape

export type SensorCategory = 'Environmental' | 'PowerConsumption' | 'Actuator'

export type ThresholdBand = 'Normal' | 'Caution' | 'Alert'

export type LivenessState = 'Live' | 'Dimmed' | 'Stale'

export type SensorValue = number | boolean

export interface Sensor {
  nodeId: string
  mac: string
  location: string
  category: SensorCategory
  expectedIntervalMs: number
  registeredAtUtc: string
}

export interface SensorRegistration {
  nodeId: string
  mac: string
  location: string
  category: SensorCategory
  expectedIntervalMs: number
}

export interface Reading {
  timestampUtc: string
  value: SensorValue
}

export interface NodeHistory {
  nodeId: string
  count: number
  readings: Reading[]
}

export interface ZoneSummary {
  name: string
  sensorCount: number
  totalWatts: number
  actuatorCount: number
  allActuatorsOpen: boolean
  anyActuatorOpen: boolean
  nodeIds: string[]
  children: ZoneSummary[]
}

export interface Topology {
  root: ZoneSummary
  validationErrors: string[]
}

export interface Attachment {
  id: string
  nodeId: string
  fileName: string
  contentType: string
  sizeBytes: number
  uploadedAtUtc: string
}

// Pushed over SignalR when a reading arrives
export interface ReadingEvent {
  nodeId: string
  timestampUtc: string
  value: SensorValue
  band: ThresholdBand
}

// Pushed when the sweeper notices a node went quiet, or started reporting again
export interface LivenessEvent {
  nodeId: string
  liveness: LivenessState
  lastSeenUtc: string | null
}

// The shape ProblemDetails uses for field level validation failures
export interface ValidationProblem {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}
