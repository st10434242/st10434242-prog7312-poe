namespace SmartX.Core.Contracts;

// Pushed over SignalR when a reading arrives
public sealed record ReadingEventDto
{
    public required string NodeId { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    // object boxes the value, which is fine here because it is about to be serialised anyway
    public required object Value { get; init; }

    public required ThresholdBand Band { get; init; }
}

// Pushed when the sweeper notices a node has gone quiet, or started reporting again
public sealed record LivenessEventDto
{
    public required string NodeId { get; init; }

    public required LivenessState Liveness { get; init; }

    public required DateTimeOffset? LastSeenUtc { get; init; }
}
