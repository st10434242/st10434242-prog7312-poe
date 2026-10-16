using System.Text.Json.Serialization;

namespace SmartX.Core;

// Drives tile hue on the wall
[JsonConverter(typeof(JsonStringEnumConverter<ThresholdBand>))]
public enum ThresholdBand
{
    Normal = 1,
    Caution = 2,
    Alert = 3
}

// Drives tile opacity and pulse on the wall
[JsonConverter(typeof(JsonStringEnumConverter<LivenessState>))]
public enum LivenessState
{
    Live = 1,
    Dimmed = 2,
    Stale = 3
}

public static class NodeStatus
{
    // A node is dimmed after missing one expected publish and stale after missing three
    public const int DimmedAfterIntervals = 1;
    public const int StaleAfterIntervals = 3;

    public static LivenessState LivenessFor(
        DateTimeOffset? lastSeenUtc,
        TimeSpan expectedInterval,
        DateTimeOffset now)
    {
        // Never heard from at all counts as stale, not as healthy
        if (lastSeenUtc is null)
        {
            return LivenessState.Stale;
        }

        TimeSpan age = now - lastSeenUtc.Value;

        if (age <= expectedInterval * DimmedAfterIntervals)
        {
            return LivenessState.Live;
        }

        return age <= expectedInterval * StaleAfterIntervals
            ? LivenessState.Dimmed
            : LivenessState.Stale;
    }

    public static ThresholdBand BandFor(SensorCategory category, double value) => category switch
    {
        SensorCategory.Environmental => value switch
        {
            >= 80 => ThresholdBand.Alert,
            >= 60 => ThresholdBand.Caution,
            _ => ThresholdBand.Normal
        },
        SensorCategory.PowerConsumption => value switch
        {
            >= 1000 => ThresholdBand.Alert,
            >= 800 => ThresholdBand.Caution,
            _ => ThresholdBand.Normal
        },
        // A valve being open is a state, not a severity
        _ => ThresholdBand.Normal
    };
}
