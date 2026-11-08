namespace SmartX.Core;

// A live node whose usual way to the gateway runs through something that is down
public sealed record Reroute
{
    public required string NodeId { get; init; }

    public required IReadOnlyList<string> BlockedBy { get; init; }

    public required string? OldNextHop { get; init; }

    public required int OldHops { get; init; }

    // Both null means there is no way out at all and the node is orphaned
    public required string? NewNextHop { get; init; }

    public required int? NewHops { get; init; }
}

public static class MeshReachability
{
    // Up but unreachable, every path it has to the gateway goes through a down node
    public static IReadOnlyList<string> Orphans(MeshGraph graph, BfsResult fromGateway, Func<string, bool> isDown) =>
        [.. graph.Vertices
            .Where(nodeId => !isDown(nodeId) && !fromGateway.Reached(nodeId))
            .Order(StringComparer.OrdinalIgnoreCase)];

    // Runs BFS twice, once pretending everything is healthy and once for real, then compares the two
    public static IReadOnlyList<Reroute> FindReroutes(MeshGraph graph, Func<string, bool> isDown)
    {
        BfsResult healthy = MeshTraversal.Bfs(graph, MeshRadio.GatewayId, _ => false);
        BfsResult live = MeshTraversal.Bfs(graph, MeshRadio.GatewayId, isDown);

        List<Reroute> reroutes = [];

        foreach (string nodeId in healthy.VisitOrder)
        {
            if (isDown(nodeId))
            {
                continue;
            }

            List<string> blockedBy = [.. healthy.PathTo(nodeId).Where(isDown)];

            if (blockedBy.Count == 0)
            {
                continue;
            }

            reroutes.Add(new Reroute
            {
                NodeId = nodeId,
                BlockedBy = blockedBy,
                OldNextHop = healthy.ParentOf(nodeId),
                OldHops = healthy.HopsTo(nodeId) ?? 0,
                NewNextHop = live.ParentOf(nodeId),
                NewHops = live.HopsTo(nodeId)
            });
        }

        return reroutes;
    }
}
