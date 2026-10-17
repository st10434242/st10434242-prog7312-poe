import type { ReactNode } from 'react'
import { useFleet } from '../hooks/useFleet'
import { FleetContext } from './fleetContext'

// Lives above the routes so the wall keeps updating while you are on another page
export function FleetProvider({ children }: { children: ReactNode }) {
  const fleet = useFleet()

  return <FleetContext.Provider value={fleet}>{children}</FleetContext.Provider>
}
