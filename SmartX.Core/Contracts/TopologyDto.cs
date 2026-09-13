namespace SmartX.Core.Contracts;

public sealed record TopologyDto
{
    public required ZoneSummary Root { get; init; }

    public required IReadOnlyList<string> ValidationErrors { get; init; }
}
