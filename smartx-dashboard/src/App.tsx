import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AppShell } from './components/AppShell'
import { FleetProvider } from './context/FleetProvider'
import { HubProvider } from './context/HubProvider'
import { CommandStreamPage } from './pages/CommandStreamPage'
import { LandingPage } from './pages/LandingPage'
import { NodeDetailPage } from './pages/NodeDetailPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { TelemetryPage } from './pages/TelemetryPage'

// Providers sit above the router so the connection and fleet state outlive any one page
export default function App() {
  return (
    <HubProvider>
      <FleetProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<AppShell />}>
              <Route path="/" element={<LandingPage />} />
              <Route path="/telemetry" element={<TelemetryPage />} />
              <Route path="/commands" element={<CommandStreamPage />} />
              <Route path="/node/:nodeId" element={<NodeDetailPage />} />
              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </FleetProvider>
    </HubProvider>
  )
}
