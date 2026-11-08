namespace SmartX.Core;

// One wireless connection between two nodes, the edge of the mesh graph
public sealed record MeshLink
{
    public required string From { get; init; }

    public required string To { get; init; }

    public required int RssiDbm { get; init; }

    public DateTimeOffset LastReportedUtc { get; init; } = DateTimeOffset.UtcNow;

    // Worked out from the signal so a weak link is always the more expensive one
    public double Cost => MeshRadio.CostFor(RssiDbm);

    // Lets either end look up the node on the other side
    public string Other(string nodeId) =>
        string.Equals(nodeId, From, StringComparison.OrdinalIgnoreCase) ? To : From;
}
