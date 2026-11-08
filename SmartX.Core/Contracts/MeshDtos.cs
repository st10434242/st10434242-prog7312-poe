using System.Text.Json.Serialization;

namespace SmartX.Core.Contracts;

// A neighbour the reporting node can hear and how strong it sounds
public sealed record NeighbourDto
{
    public required string NodeId { get; init; }

    public required int RssiDbm { get; init; }
}

// What a node beacons to the gateway every so often, its spot on the floor and who it can hear
public sealed record NeighbourReportDto
{
    public required string NodeId { get; init; }

    // Metres from the facility origin, only used to draw the map
    public required double X { get; init; }

    public required double Y { get; init; }

    public required IReadOnlyList<NeighbourDto> Neighbours { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<MeshNodeState>))]
public enum MeshNodeState
{
    Up = 1,
    Down = 2,

    // Still running but cut off from the gateway because everything around it is down
    Orphaned = 3
}

public sealed record MeshNodeDto
{
    public required string NodeId { get; init; }

    // Null for the gateway since it isn't a registered sensor
    public string? Mac { get; init; }

    public string? Location { get; init; }

    public SensorCategory? Category { get; init; }

    public required double X { get; init; }

    public required double Y { get; init; }

    public required bool IsGateway { get; init; }

    public required MeshNodeState State { get; init; }
}

public sealed record MeshEdgeDto
{
    public required string From { get; init; }

    public required string To { get; init; }

    public required int RssiDbm { get; init; }

    public required double Cost { get; init; }

    public static MeshEdgeDto FromLink(MeshLink link) => new()
    {
        From = link.From,
        To = link.To,
        RssiDbm = link.RssiDbm,
        Cost = link.Cost
    };
}

public sealed record MeshSnapshotDto
{
    public required IReadOnlyList<MeshNodeDto> Nodes { get; init; }

    public required IReadOnlyList<MeshEdgeDto> Edges { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ReachedNodeDto
{
    public required string NodeId { get; init; }

    public required int Hops { get; init; }

    // The node it was reached through, null for the start
    public string? Via { get; init; }
}

public sealed record ReachDto
{
    public required string Start { get; init; }

    public string? Zone { get; init; }

    // In the order BFS visited them, so closest first
    public required IReadOnlyList<ReachedNodeDto> Connected { get; init; }

    public required IReadOnlyList<string> Down { get; init; }

    public required IReadOnlyList<string> Orphaned { get; init; }
}

public sealed record RerouteDto
{
    public required string NodeId { get; init; }

    public required IReadOnlyList<string> BlockedBy { get; init; }

    public string? OldNextHop { get; init; }

    public required int OldHops { get; init; }

    public string? NewNextHop { get; init; }

    public int? NewHops { get; init; }

    public bool IsOrphaned => NewNextHop is null;

    public static RerouteDto FromReroute(Reroute reroute) => new()
    {
        NodeId = reroute.NodeId,
        BlockedBy = reroute.BlockedBy,
        OldNextHop = reroute.OldNextHop,
        OldHops = reroute.OldHops,
        NewNextHop = reroute.NewNextHop,
        NewHops = reroute.NewHops
    };
}
