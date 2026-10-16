using System.Text.Json.Serialization;

namespace SmartX.Core.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<TimelineEventKind>))]
public enum TimelineEventKind
{
    Alert = 1,
    Command = 2,
    Undo = 3,
    Disconnected = 4,
    Online = 5
}

// One point on a device's graph, valves come through as 0 or 1
public sealed record TimelinePointDto(DateTimeOffset TimestampUtc, double Value);

public sealed record NodeTimelineDto
{
    public required string NodeId { get; init; }

    public required int Count { get; init; }

    public required IReadOnlyList<TimelinePointDto> Points { get; init; }
}

// Something that happened on the network, shown in the history feed
public sealed record TimelineEventDto
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required DateTimeOffset TimestampUtc { get; init; }

    public required TimelineEventKind Kind { get; init; }

    public string? NodeId { get; init; }

    public AlertSeverity? Severity { get; init; }

    public required string Message { get; init; }
}
