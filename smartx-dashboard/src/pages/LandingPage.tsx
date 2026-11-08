import { Link } from 'react-router-dom'
import { modules, type Module } from '../lib/modules'
import './LandingPage.css'

export function LandingPage() {
  return (
    <main className="page">
      <p className="landing__brand">Smart-X</p>
      <h1 className="landing__title">IoT Mesh Ecosystem</h1>
      <p className="landing__subtitle">
        Ingestion gateway, real-time command stream and self-healing mesh routing for distributed ESP32 sensor networks.
      </p>

      <div className="pillars">
        {modules.map((pillar, index) => (
          <PillarCard key={pillar.name} pillar={pillar} index={index + 1} />
        ))}
      </div>
    </main>
  )
}

function PillarCard({ pillar, index }: { pillar: Module; index: number }) {
  const body = (
    <>
      <span className="pillar__index">{String(index).padStart(2, '0')}</span>
      <h2 className="pillar__name">{pillar.name}</h2>
      <p className="pillar__description">{pillar.description}</p>
      <span className="pillar__status pillar__status--available">Open module</span>
    </>
  )

  return (
    <Link to={pillar.route} className="pillar pillar--available">
      {body}
    </Link>
  )
}
