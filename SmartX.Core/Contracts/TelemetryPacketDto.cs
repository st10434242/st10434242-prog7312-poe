using System.Text.Json;

namespace SmartX.Core.Contracts;

// The wire shape of a reading, shared so the simulator and gateway agree on the format
public sealed record TelemetryPacketDto
{
    public required string NodeId { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    // Sent even though the gateway knows it already, so a misconfigured device can be spotted
    public required SensorCategory Category { get; init; }

    public required TelemetryValueKind ValueKind { get; init; }

    // Left as raw JSON until ValueKind says how to read it
    public required JsonElement Value { get; init; }

    public bool TryReadFloat(out float value)
    {
        value = default;
        return Value.ValueKind == JsonValueKind.Number && Value.TryGetSingle(out value);
    }

    public bool TryReadInt(out int value)
    {
        value = default;
        return Value.ValueKind == JsonValueKind.Number && Value.TryGetInt32(out value);
    }

    public bool TryReadBool(out bool value)
    {
        switch (Value.ValueKind)
        {
            case JsonValueKind.True:
                value = true;
                return true;

            case JsonValueKind.False:
                value = false;
                return true;

            default:
                value = default;
                return false;
        }
    }

    // Rejects a payload whose tag and value disagree, like valueKind Bool with a value of 42
    public bool HasConsistentValue() => ValueKind switch
    {
        TelemetryValueKind.Float => TryReadFloat(out _),
        TelemetryValueKind.Int => TryReadInt(out _),
        TelemetryValueKind.Bool => TryReadBool(out _),
        _ => false
    };

    public static TelemetryValueKind ExpectedKindFor(SensorCategory category) => category switch
    {
        SensorCategory.Environmental => TelemetryValueKind.Float,
        SensorCategory.PowerConsumption => TelemetryValueKind.Int,
        SensorCategory.Actuator => TelemetryValueKind.Bool,
        _ => TelemetryValueKind.Unspecified
    };
}
