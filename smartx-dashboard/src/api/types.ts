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
  liveness: LivenessState
  lastSeenUtc: string | null
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

// Part 2, the command stream contracts

export type AlertKind = 'PowerSpike' | 'MoistureDrop' | 'MoistureFlood' | 'Disconnected'

export type AlertSeverity = 'Info' | 'Warning' | 'Critical'

export type DeviceAlertState = 'Normal' | 'Caution' | 'Alert' | 'Disconnected'

export type ActuatorKind = 'Valve' | 'Pump' | 'Relay' | 'Generic'

export type CommandType =
  | 'OpenValve'
  | 'CloseValve'
  | 'StartPump'
  | 'StopPump'
  | 'RelayOn'
  | 'RelayOff'
  | 'ShutAllValves'

export type TimelineEventKind = 'Alert' | 'Command' | 'Undo' | 'Disconnected' | 'Online'

export interface QueueStats {
  depth: number
  capacity: number
  enqueued: number
  processed: number
  rejected: number
  packetsPerSecond: number
  alertDepth: number
  alertsProcessed: number
  criticalAlerts: number
  warningAlerts: number
}

export interface Alert {
  id: string
  nodeId: string
  kind: AlertKind
  severity: AlertSeverity
  value: number | null
  message: string
  raisedAtUtc: string
}

export interface TimelinePoint {
  timestampUtc: string
  value: number
}

export interface NodeTimeline {
  nodeId: string
  count: number
  points: TimelinePoint[]
}

export interface TimelineEvent {
  id: string
  timestampUtc: string
  kind: TimelineEventKind
  nodeId: string | null
  severity: AlertSeverity | null
  message: string
}

export interface ActiveError {
  nodeId: string
  kind: AlertKind
  severity: AlertSeverity
}

export interface Faults {
  disconnected: string[]
  activeErrors: ActiveError[]
  suppressedDuplicates: number
}

export interface Device {
  nodeId: string
  location: string
  category: SensorCategory
  lastValue: number | null
  isOn: boolean | null
  commandedState: boolean | null
  actuator: ActuatorKind | null
  band: ThresholdBand
  liveness: LivenessState
  alertState: DeviceAlertState
  lastSeenUtc: string | null
  packetCount: number
  activeAlerts: number
}

export interface DeviceFilter {
  category?: SensorCategory
  alertState?: DeviceAlertState
  search?: string
}

export interface IssueCommand {
  nodeId?: string | null
  type: CommandType
}

export interface CommandRecord {
  id: string
  type: CommandType
  nodeIds: string[]
  desiredState: boolean
  issuedAtUtc: string
  reverted: boolean
}

export interface CommandHistory {
  undoDepth: number
  nextUndo: CommandRecord | null
  commands: CommandRecord[]
}

export type InsightKind = 'SuggestedAction' | 'Highlight'

export type InsightSource = 'Rule' | 'Learned' | 'Combined'

export interface Insight {
  id: string
  kind: InsightKind
  source: InsightSource
  title: string
  reason: string
  score: number
  severity: AlertSeverity | null
  nodeId: string
  triggerNodeId: string | null
  command: IssueCommand | null
}

export interface Activity {
  kind: 'Search' | 'View'
  term: string
}

export interface Pattern {
  from: string
  to: string
  count: number
  confidence: number
}

// Network topology and mesh routing

export type MeshNodeState = 'Up' | 'Down' | 'Orphaned'

export type MstAlgorithm = 'Prim' | 'Kruskal'

export interface MeshNode {
  nodeId: string
  mac: string | null
  location: string | null
  category: SensorCategory | null
  x: number
  y: number
  isGateway: boolean
  state: MeshNodeState
}

export interface MeshEdge {
  from: string
  to: string
  rssiDbm: number
  cost: number
}

export interface MeshSnapshot {
  nodes: MeshNode[]
  edges: MeshEdge[]
  generatedAtUtc: string
}

export interface ReachedNode {
  nodeId: string
  hops: number
  via: string | null
}

export interface Reach {
  start: string
  zone: string | null
  connected: ReachedNode[]
  down: string[]
  orphaned: string[]
}

export interface MeshReroute {
  nodeId: string
  blockedBy: string[]
  oldNextHop: string | null
  oldHops: number
  newNextHop: string | null
  newHops: number | null
  isOrphaned: boolean
}

export interface SpanningTree {
  root: string
  algorithm: string
  edges: MeshEdge[]
  totalCost: number
  spannedCount: number
  unspanned: string[]
  computeMicroseconds: number
}

export interface MstComparison {
  primTotalCost: number
  kruskalTotalCost: number
  primMicroseconds: number
  kruskalMicroseconds: number
  edgeCount: number
  sharedEdges: number
  match: boolean
}

export interface RouteHop {
  from: string
  to: string
  rssiDbm: number
  cost: number
}

export interface RoutePath {
  algorithm: string
  hops: RouteHop[]
  hopCount: number
  totalCost: number
}

export interface MeshRoute {
  nodeId: string
  mac: string | null
  state: MeshNodeState
  reachable: boolean
  backbone: RoutePath
  shortest: RoutePath
  blockedBy: string[]
  usual: RoutePath
  rerouted: boolean
}

export interface NodeStateChange {
  nodeId: string
  from: MeshNodeState | null
  to: MeshNodeState | null
}

export interface MeshChanged {
  up: number
  down: number
  orphaned: number
  links: number
  backboneCost: number
  changes: NodeStateChange[]
  linksChanged: boolean
  changedAtUtc: string
}

// Historical telemetry from the AVL index

export interface HistoryCoverage {
  nodeId: string
  mac: string
  category: SensorCategory
  readings: number
  oldestUtc: string | null
  newestUtc: string | null
}

export interface HistoryPoint {
  timestampUtc: string
  value: number
}

export interface HistoryRange {
  nodeId: string
  mac: string
  category: SensorCategory
  fromUtc: string
  toUtc: string
  points: HistoryPoint[]
  downsampled: boolean
  count: number
  min: number | null
  max: number | null
  average: number | null
  nodesVisited: number
  treeSize: number
  treeHeight: number
  queryMicroseconds: number
}

export interface BenchmarkRow {
  size: number
  windowMinutes: number
  treeHeight: number
  buildMilliseconds: number
  averageResults: number
  avlMicroseconds: number
  avlNodesVisited: number
  listMicroseconds: number
  listItemsScanned: number
  speedup: number
}
