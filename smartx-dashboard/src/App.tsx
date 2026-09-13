import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { LandingPage } from './pages/LandingPage'
import { NodeDetailPage } from './pages/NodeDetailPage'
import { TelemetryPage } from './pages/TelemetryPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/telemetry" element={<TelemetryPage />} />
        <Route path="/node/:nodeId" element={<NodeDetailPage />} />
      </Routes>
    </BrowserRouter>
  )
}
