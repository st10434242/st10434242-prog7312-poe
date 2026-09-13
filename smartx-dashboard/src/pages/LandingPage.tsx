import { Link } from 'react-router-dom'
import './LandingPage.css'

interface Pillar {
  name: string
  description: string
  route?: string
  lockedUntil?: string
}

// The three architectural pillars from the brief, two of them not built yet
const pillars: Pillar[] = [
  {
    name: 'Sensor Data Ingestion and Telemetry',
    description:
      'Register devices, ingest multi-typed telemetry and watch the fleet live on the Heartbeat Wall.',
    route: '/telemetry',
  },
  {
    name: 'Real-Time Command Stream and History',
    description: 'Push commands down to devices and replay what the mesh has been told to do.',
    lockedUntil: 'Part 2',
  },
  {
    name: 'Network Topology and Mesh Routing',
    description: 'Trace how packets hop between nodes and where the mesh is routing around failures.',
    lockedUntil: 'Final PoE',
  },
]

export function LandingPage() {
  return (
    <main className="page">
      <p className="landing__brand">Smart-X</p>
      <h1 className="landing__title">IoT Mesh Ecosystem</h1>
      <p className="landing__subtitle">
        Data ingestion and validation gateway for distributed ESP32 sensor networks.
      </p>

      <div className="pillars">
        {pillars.map((pillar, index) => (
          <PillarCard key={pillar.name} pillar={pillar} index={index + 1} />
        ))}
      </div>
    </main>
  )
}

function PillarCard({ pillar, index }: { pillar: Pillar; index: number }) {
  const body = (
    <>
      <span className="pillar__index">{String(index).padStart(2, '0')}</span>
      <h2 className="pillar__name">{pillar.name}</h2>
      <p className="pillar__description">{pillar.description}</p>
      <span
        className={`pillar__status pillar__status--${pillar.route ? 'available' : 'locked'}`}
      >
        {pillar.route ? 'Available' : `Coming in ${pillar.lockedUntil}`}
      </span>
    </>
  )

  // Locked pillars stay visible so the full roadmap is legible without being reachable
  if (!pillar.route) {
    return (
      <div className="pillar pillar--locked" aria-disabled="true">
        {body}
      </div>
    )
  }

  return (
    <Link to={pillar.route} className="pillar pillar--available">
      {body}
    </Link>
  )
}
