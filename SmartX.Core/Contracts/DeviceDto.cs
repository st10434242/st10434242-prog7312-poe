namespace SmartX.Core.Contracts;

// A copy of DeviceState so the table can keep changing while this gets serialised
public sealed record DeviceDto
{
    public required string NodeId { get; init; }

    public required string Location { get; init; }

    public required SensorCategory Category { get; init; }

    public double? LastValue { get; init; }

    public bool? IsOn { get; init; }

    public required ThresholdBand Band { get; init; }

    public required LivenessState Liveness { get; init; }

    public required DeviceAlertState AlertState { get; init; }

    public DateTimeOffset? LastSeenUtc { get; init; }

    public required long PacketCount { get; init; }

    public required int ActiveAlerts { get; init; }

    public static DeviceDto From(DeviceState state) => new()
    {
        NodeId = state.NodeId,
        Location = state.Location,
        Category = state.Category,
        LastValue = state.LastValue,
        IsOn = state.IsOn,
        Band = state.Band,
        Liveness = state.Liveness,
        AlertState = state.AlertState,
        LastSeenUtc = state.LastSeenUtc,
        PacketCount = state.PacketCount,
        ActiveAlerts = state.ActiveAlerts
    };
}
