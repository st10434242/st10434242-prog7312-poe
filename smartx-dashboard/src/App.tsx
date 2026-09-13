import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { LandingPage } from './pages/LandingPage'
import { TelemetryPage } from './pages/TelemetryPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/telemetry" element={<TelemetryPage />} />
      </Routes>
    </BrowserRouter>
  )
}
