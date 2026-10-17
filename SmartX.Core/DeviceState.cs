using System.Text.Json.Serialization;

namespace SmartX.Core;

// What the filter on the command stream page works with, disconnected wins over whatever the last reading said
[JsonConverter(typeof(JsonStringEnumConverter<DeviceAlertState>))]
public enum DeviceAlertState
{
    Normal = 1,
    Caution = 2,
    Alert = 3,
    Disconnected = 4
}

// The live picture of one device, only ever touched inside the DeviceTable lock
public sealed class DeviceState
{
    public required string NodeId { get; init; }

    public required string Location { get; init; }

    public required SensorCategory Category { get; init; }

    public double? LastValue { get; set; }

    // Only used by actuators, null until the valve or pump has reported once
    public bool? IsOn { get; set; }

    // What the operator last asked for, can differ from IsOn until the device picks the command up
    public bool? CommandedState { get; set; }

    public ThresholdBand Band { get; set; } = ThresholdBand.Normal;

    public LivenessState Liveness { get; set; } = LivenessState.Stale;

    public DateTimeOffset? LastSeenUtc { get; set; }

    public long PacketCount { get; set; }

    // Alerts since the device last read normal, so it drops back to 0 once things settle
    public int ActiveAlerts { get; set; }

    public DeviceAlertState AlertState => Liveness == LivenessState.Stale
        ? DeviceAlertState.Disconnected
        : Band switch
        {
            ThresholdBand.Alert => DeviceAlertState.Alert,
            ThresholdBand.Caution => DeviceAlertState.Caution,
            _ => DeviceAlertState.Normal
        };
}
