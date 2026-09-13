namespace SmartX.Core.Contracts;

public sealed record NodeHistoryDto
{
    public required string NodeId { get; init; }

    public required int Count { get; init; }

    public required IReadOnlyList<ReadingDto> Readings { get; init; }
}

public sealed record ReadingDto
{
    public required DateTimeOffset TimestampUtc { get; init; }

    // object boxes the value, which is fine here because it is about to be serialised anyway
    public required object Value { get; init; }
}
