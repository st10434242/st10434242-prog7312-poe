using System.Text.Json.Serialization;

namespace SmartX.Core;

[JsonConverter(typeof(JsonStringEnumConverter<SensorCategory>))]
public enum SensorCategory
{
    // Unspecified is 0 so a value left at its default is invalid
    Unspecified = 0,
    Environmental = 1,
    PowerConsumption = 2,
    Actuator = 3
}
