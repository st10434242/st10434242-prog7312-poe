export interface Module {
  name: string
  shortName: string
  description: string
  route: string
}

// The three architectural pillars from the brief, shared by the landing page and the nav bar
export const modules: Module[] = [
  {
    name: 'Sensor Data Ingestion and Telemetry',
    shortName: 'Telemetry',
    description:
      'Register devices, ingest multi-typed telemetry and watch the fleet live on the Heartbeat Wall.',
    route: '/telemetry',
  },
  {
    name: 'Real-Time Command Stream and History',
    shortName: 'Command Stream',
    description:
      'Watch packets and alerts flow through the gateway, override actuators with undo, and act on suggested fixes.',
    route: '/commands',
  },
  {
    name: 'Network Topology and Mesh Routing',
    shortName: 'Topology',
    description:
      'Map the mesh, trace any MAC back to the gateway, see who is cut off when a relay drops and browse past telemetry.',
    route: '/topology',
  },
]
