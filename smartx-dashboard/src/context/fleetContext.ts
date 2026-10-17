import { createContext, useContext } from 'react'
import type { Fleet } from '../hooks/useFleet'

export const FleetContext = createContext<Fleet | null>(null)

export function useFleetContext(): Fleet {
  const fleet = useContext(FleetContext)

  if (!fleet) {
    throw new Error('useFleetContext has to be used inside FleetProvider')
  }

  return fleet
}
