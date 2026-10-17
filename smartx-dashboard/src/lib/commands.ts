import type { ActuatorKind, CommandRecord, CommandType, Device } from '../api/types'

export const commandLabels: Record<CommandType, string> = {
  OpenValve: 'Open valve',
  CloseValve: 'Close valve',
  StartPump: 'Start pump',
  StopPump: 'Stop pump',
  RelayOn: 'Switch on',
  RelayOff: 'Cut power',
  ShutAllValves: 'Shut all valves',
}

// The command that moves an actuator to the state we want, depends on what kind of device it is
export function commandFor(kind: ActuatorKind | null, turnOn: boolean): CommandType {
  switch (kind) {
    case 'Valve':
      return turnOn ? 'OpenValve' : 'CloseValve'
    case 'Pump':
      return turnOn ? 'StartPump' : 'StopPump'
    default:
      return turnOn ? 'RelayOn' : 'RelayOff'
  }
}

// What the operator asked for wins over the last report, otherwise the button flips back until the device catches up
export function effectiveState(device: Device): boolean {
  return device.commandedState ?? device.isOn ?? false
}

export function isPending(device: Device): boolean {
  return device.commandedState !== null && device.isOn !== device.commandedState
}

export function describeTargets(record: CommandRecord): string {
  return record.nodeIds.length === 1 ? record.nodeIds[0] : `${record.nodeIds.length} devices`
}
