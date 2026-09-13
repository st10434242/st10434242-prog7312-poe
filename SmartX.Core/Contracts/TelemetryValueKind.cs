using System.Text.Json.Serialization;

namespace SmartX.Core.Contracts;

// JSON can't tell a float from an int, so the payload tags itself and the gateway uses this to build the right closed generic
[JsonConverter(typeof(JsonStringEnumConverter<TelemetryValueKind>))]
public enum TelemetryValueKind
{
    Unspecified = 0,
    Float = 1,
    Int = 2,
    Bool = 3
}
