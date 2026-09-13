namespace SmartX.Core.Contracts;

// Mac and Location are strings on the wire, then parsed into domain types at the boundary
public sealed record SensorRegistrationDto
{
    public required string NodeId { get; init; }

    public required string Mac { get; init; }

    public required string Location { get; init; }

    public required SensorCategory Category { get; init; }

    public required int ExpectedIntervalMs { get; init; }
}

public sealed record SensorDto
{
    public required string NodeId { get; init; }

    public required string Mac { get; init; }

    public required string Location { get; init; }

    public required SensorCategory Category { get; init; }

    public required int ExpectedIntervalMs { get; init; }

    public required DateTimeOffset RegisteredAtUtc { get; init; }
}

public sealed record BatchResultDto
{
    public required int Accepted { get; init; }

    public required int Rejected { get; init; }
}
