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

// Either the MST backbone or the Dijkstra shortest path tree, both are rooted at the gateway
public sealed record SpanningTreeDto
{
    public required string Root { get; init; }

    public required string Algorithm { get; init; }

    public required IReadOnlyList<MeshEdgeDto> Edges { get; init; }

    // Sum of every link in the tree, for the MST this is the radio energy to keep everyone connected
    public required double TotalCost { get; init; }

    public required int SpannedCount { get; init; }

    public required IReadOnlyList<string> Unspanned { get; init; }

    public required double ComputeMicroseconds { get; init; }

    public static SpanningTreeDto FromTree(SpanningTree tree, TimeSpan elapsed) => new()
    {
        Root = tree.Root,
        Algorithm = tree.Algorithm,
        Edges = [.. tree.Edges.Select(MeshEdgeDto.FromLink)],
        TotalCost = tree.TotalCost,
        SpannedCount = tree.SpannedCount,
        Unspanned = tree.Unspanned,
        ComputeMicroseconds = Math.Round(elapsed.TotalMicroseconds, 1)
    };
}

public sealed record RouteHopDto
{
    public required string From { get; init; }

    public required string To { get; init; }

    public required int RssiDbm { get; init; }

    public required double Cost { get; init; }
}

// One way home for a node, from the node to the gateway
public sealed record RoutePathDto
{
    public required string Algorithm { get; init; }

    public required IReadOnlyList<RouteHopDto> Hops { get; init; }

    public int HopCount => Hops.Count;

    public double TotalCost => Math.Round(Hops.Sum(h => h.Cost), 2);

    public static RoutePathDto FromTree(SpanningTree tree, string nodeId) => new()
    {
        Algorithm = tree.Algorithm,
        Hops = [.. tree.RouteToRoot(nodeId).Select(hop => new RouteHopDto
        {
            From = hop.From,
            To = hop.To,
            RssiDbm = hop.RssiDbm,
            Cost = hop.Cost
        })]
    };
}

public sealed record RouteDto
{
    public required string NodeId { get; init; }

    public string? Mac { get; init; }

    public required MeshNodeState State { get; init; }

    // False when the node is down or orphaned and has no route home
    public required bool Reachable { get; init; }

    // The route along the MST, part of the cheapest network overall
    public required RoutePathDto Backbone { get; init; }

    // The cheapest route for this node on its own
    public required RoutePathDto Shortest { get; init; }

    // Down nodes sitting on the route it would normally take, empty when nothing is in the way
    public required IReadOnlyList<string> BlockedBy { get; init; }

    // The shortest route as if every node was up, so the dashboard can show what it healed around
    public required RoutePathDto Usual { get; init; }

    public bool Rerouted => BlockedBy.Count > 0;
}

// Prim and Kruskal run on the same snapshot, a correct MST has one total cost even if ties pick different links
public sealed record MstComparisonDto
{
    public required double PrimTotalCost { get; init; }

    public required double KruskalTotalCost { get; init; }

    public required double PrimMicroseconds { get; init; }

    public required double KruskalMicroseconds { get; init; }

    public required int EdgeCount { get; init; }

    public required int SharedEdges { get; init; }

    // Small tolerance since the two add the same costs up in a different order
    public bool Match => Math.Abs(PrimTotalCost - KruskalTotalCost) < 0.01;
}

public sealed record NodeStateChangeDto
{
    public required string NodeId { get; init; }

    // Null when the node has only just appeared in the mesh
    public MeshNodeState? From { get; init; }

    // Null when the node has been removed from the mesh
    public MeshNodeState? To { get; init; }
}

// Pushed over SignalR when the mesh changes, the dashboard reloads the map when it gets one
public sealed record MeshChangedDto
{
    public required int Up { get; init; }

    public required int Down { get; init; }

    public required int Orphaned { get; init; }

    public required int Links { get; init; }

    public required double BackboneCost { get; init; }

    public required IReadOnlyList<NodeStateChangeDto> Changes { get; init; }

    public required bool LinksChanged { get; init; }

    public DateTimeOffset ChangedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
