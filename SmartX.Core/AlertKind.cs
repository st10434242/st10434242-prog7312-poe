using System.Text.Json.Serialization;

namespace SmartX.Core;

[JsonConverter(typeof(JsonStringEnumConverter<AlertKind>))]
public enum AlertKind
{
    PowerSpike = 1,
    MoistureDrop = 2,
    MoistureFlood = 3,
    Disconnected = 4
}

// Higher number is worse, the priority queue flips it so critical comes out first
[JsonConverter(typeof(JsonStringEnumConverter<AlertSeverity>))]
public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}
