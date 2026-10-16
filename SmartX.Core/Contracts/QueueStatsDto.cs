namespace SmartX.Core.Contracts;

// Shown on the command stream page so you can see the queue keeping up under load
public sealed record QueueStatsDto
{
    public required int Depth { get; init; }

    public required int Capacity { get; init; }

    public required long Enqueued { get; init; }

    public required long Processed { get; init; }

    public required long Rejected { get; init; }

    public required int PacketsPerSecond { get; init; }
}
