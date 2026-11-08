namespace SmartX.Core.Contracts;

public sealed record HistoryPointDto(DateTimeOffset TimestampUtc, double Value);

// A node in the history picker and how far back its history goes
public sealed record HistoryCoverageDto
{
    public required string NodeId { get; init; }

    public required string Mac { get; init; }

    public required SensorCategory Category { get; init; }

    public required int Readings { get; init; }

    public DateTimeOffset? OldestUtc { get; init; }

    public DateTimeOffset? NewestUtc { get; init; }
}

public sealed record HistoryDto
{
    public required string NodeId { get; init; }

    public required string Mac { get; init; }

    public required SensorCategory Category { get; init; }

    public required DateTimeOffset FromUtc { get; init; }

    public required DateTimeOffset ToUtc { get; init; }

    // Every reading in the window, unless there were more than the limit and they were thinned out
    public required IReadOnlyList<HistoryPointDto> Points { get; init; }

    public required bool Downsampled { get; init; }

    // Worked out over every reading in the window, even when the points were thinned out
    public required int Count { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public double? Average { get; init; }

    public required int NodesVisited { get; init; }

    public required int TreeSize { get; init; }

    public required int TreeHeight { get; init; }

    public required double QueryMicroseconds { get; init; }
}

public sealed record BenchmarkRowDto
{
    public required int Size { get; init; }

    public required double WindowMinutes { get; init; }

    public required int TreeHeight { get; init; }

    public required double BuildMilliseconds { get; init; }

    public required long AverageResults { get; init; }

    public required double AvlMicroseconds { get; init; }

    public required long AvlNodesVisited { get; init; }

    public required double ListMicroseconds { get; init; }

    public required long ListItemsScanned { get; init; }

    // How many times faster the tree was
    public required double Speedup { get; init; }
}
