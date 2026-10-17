using System.Text.Json.Serialization;

namespace SmartX.Core;

[JsonConverter(typeof(JsonStringEnumConverter<CommandType>))]
public enum CommandType
{
    Unspecified = 0,
    OpenValve = 1,
    CloseValve = 2,
    StartPump = 3,
    StopPump = 4,
    RelayOn = 5,
    RelayOff = 6,
    ShutAllValves = 7
}

[JsonConverter(typeof(JsonStringEnumConverter<ActuatorKind>))]
public enum ActuatorKind
{
    Generic = 0,
    Valve = 1,
    Pump = 2,
    Relay = 3
}

public static class CommandTypes
{
    // Every actuator is just on or off underneath, so each command boils down to the state it wants
    public static bool DesiredState(CommandType type) => type switch
    {
        CommandType.OpenValve => true,
        CommandType.StartPump => true,
        CommandType.RelayOn => true,
        _ => false
    };

    // Only this one hits more than one device
    public static bool IsFleetWide(CommandType type) => type == CommandType.ShutAllValves;

    public static ActuatorKind TargetKind(CommandType type) => type switch
    {
        CommandType.OpenValve or CommandType.CloseValve or CommandType.ShutAllValves => ActuatorKind.Valve,
        CommandType.StartPump or CommandType.StopPump => ActuatorKind.Pump,
        CommandType.RelayOn or CommandType.RelayOff => ActuatorKind.Relay,
        _ => ActuatorKind.Generic
    };

    // Goes off the naming convention the fleet uses, anything else is treated as a plain on/off switch
    public static ActuatorKind KindFor(string nodeId)
    {
        if (nodeId.StartsWith("VALVE", StringComparison.OrdinalIgnoreCase))
        {
            return ActuatorKind.Valve;
        }

        if (nodeId.StartsWith("PUMP", StringComparison.OrdinalIgnoreCase))
        {
            return ActuatorKind.Pump;
        }

        if (nodeId.StartsWith("RELAY", StringComparison.OrdinalIgnoreCase))
        {
            return ActuatorKind.Relay;
        }

        return ActuatorKind.Generic;
    }

    // A generic switch takes any on/off command, otherwise it has to match
    public static bool Accepts(ActuatorKind kind, CommandType type) =>
        kind == ActuatorKind.Generic || kind == TargetKind(type);
}
