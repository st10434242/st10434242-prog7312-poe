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
}
