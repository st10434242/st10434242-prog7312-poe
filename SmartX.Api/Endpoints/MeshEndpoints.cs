using System.Diagnostics;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

public static class MeshEndpoints
{
    // Anything outside this isn't a real 2.4 GHz reading
    private const int MinRssiDbm = -120;
    private const int MaxRssiDbm = 0;

    public static RouteGroupBuilder MapMeshEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/mesh").WithTags("Mesh");

        group.MapPost("/neighbours", ReportNeighbours);
        group.MapGet("/", GetMesh);
        group.MapGet("/reach", GetReach);
        group.MapGet("/reroutes", GetReroutes);
        group.MapGet("/mst", GetMst);
        group.MapGet("/mst/compare", CompareMst);
        group.MapGet("/shortest", GetShortest);
        group.MapGet("/route/{nodeIdOrMac}", GetRoute);

        return group;
    }

    private static IResult ReportNeighbours(
        NeighbourReportDto dto,
        SensorRegistry registry,
        FaultInjector faults,
        MeshNetwork network)
    {
        bool isGateway = IsGateway(dto.NodeId);

        if (!isGateway && !registry.TryGet(dto.NodeId, out _))
        {
            return Results.Problem($"Node '{dto.NodeId}' is not registered.", statusCode: 404);
        }

        Dictionary<string, string[]> errors = Validate(dto);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Same as telemetry, a silenced node's beacon is dropped so it looks like it went quiet
        if (faults.IsSilenced(dto.NodeId))
        {
            return Results.Accepted();
        }

        // Nobody can hear a silenced node either, so it gets left out of everyone else's table
        int applied = network.ApplyReport(
            dto,
            neighbour => IsGateway(neighbour) || (registry.TryGet(neighbour, out _) && !faults.IsSilenced(neighbour)),
            DateTimeOffset.UtcNow);

        return Results.Ok(new { Applied = applied });
    }

    private static IResult GetMesh(MeshNetwork network, SensorRegistry registry, MeshHealth health)
    {
        MeshView view = network.Snapshot(DateTimeOffset.UtcNow, health.IsDown);
        BfsResult fromGateway = MeshTraversal.Bfs(view.Graph, MeshRadio.GatewayId, health.IsDown);

        List<MeshNodeDto> nodes = [.. view.Graph.Vertices
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(nodeId => ToNodeDto(nodeId, view, fromGateway, registry, health))];

        List<MeshEdgeDto> edges = [.. view.Graph.Links().Select(MeshEdgeDto.FromLink)];

        return Results.Ok(new MeshSnapshotDto { Nodes = nodes, Edges = edges });
    }

    // Everything connected to a node (the gateway by default), optionally narrowed to one zone
    private static IResult GetReach(
        MeshNetwork network,
        SensorRegistry registry,
        MeshHealth health,
        string? from,
        string? zone)
    {
        if (!TryResolveVertex(from ?? MeshRadio.GatewayId, registry, out string start))
        {
            return Results.Problem($"No node matches '{from}'.", statusCode: 404);
        }

        MeshView view = network.Snapshot(DateTimeOffset.UtcNow, health.IsDown);

        if (!view.Graph.HasVertex(start))
        {
            return Results.Problem($"'{start}' hasn't reported any neighbours yet.", statusCode: 404);
        }

        BfsResult result = MeshTraversal.Bfs(view.Graph, start, health.IsDown);
        bool InZone(string nodeId) => zone is null || IsInZone(nodeId, zone, registry);

        return Results.Ok(new ReachDto
        {
            Start = start,
            Zone = zone,
            Connected = [.. result.VisitOrder
                .Where(InZone)
                .Select(nodeId => new ReachedNodeDto
                {
                    NodeId = nodeId,
                    Hops = result.HopsTo(nodeId) ?? 0,
                    Via = result.ParentOf(nodeId)
                })],
            Down = [.. view.Graph.Vertices.Where(health.IsDown).Where(InZone).Order(StringComparer.OrdinalIgnoreCase)],
            Orphaned = [.. MeshReachability.Orphans(view.Graph, result, health.IsDown).Where(InZone)]
        });
    }

    // Who lost their usual route because of a down node and where they hop now
    private static IResult GetReroutes(MeshNetwork network, MeshHealth health)
    {
        MeshView view = network.Snapshot(DateTimeOffset.UtcNow, health.IsDown);

        return Results.Ok(MeshReachability.FindReroutes(view.Graph, health.IsDown).Select(RerouteDto.FromReroute));
    }

    // Prim by default, ?algorithm=kruskal for the other one
    private static IResult GetMst(MeshNetwork network, MeshHealth health, string? algorithm)
    {
        if (algorithm is null || algorithm.Equals(PrimMst.Name, StringComparison.OrdinalIgnoreCase))
        {
            return BuildTree(network, health, PrimMst.Build);
        }

        if (algorithm.Equals(KruskalMst.Name, StringComparison.OrdinalIgnoreCase))
        {
            return BuildTree(network, health, KruskalMst.Build);
        }

        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(algorithm)] = ["Use prim or kruskal."]
        });
    }

    private static IResult CompareMst(MeshNetwork network, MeshHealth health)
    {
        MeshView view = network.Snapshot(DateTimeOffset.UtcNow, health.IsDown);

        (SpanningTree prim, TimeSpan primTime) = Timed(() => PrimMst.Build(view.Graph, MeshRadio.GatewayId, health.IsDown));
        (SpanningTree kruskal, TimeSpan kruskalTime) = Timed(() => KruskalMst.Build(view.Graph, MeshRadio.GatewayId, health.IsDown));

        // Links are shared objects in the graph so a reference match means the exact same link
        HashSet<MeshLink> primEdges = new(prim.Edges, ReferenceEqualityComparer.Instance);

        return Results.Ok(new MstComparisonDto
        {
            PrimTotalCost = prim.TotalCost,
            KruskalTotalCost = kruskal.TotalCost,
            PrimMicroseconds = Math.Round(primTime.TotalMicroseconds, 1),
            KruskalMicroseconds = Math.Round(kruskalTime.TotalMicroseconds, 1),
            EdgeCount = prim.Edges.Count,
            SharedEdges = kruskal.Edges.Count(primEdges.Contains)
        });
    }

    private static (SpanningTree Tree, TimeSpan Elapsed) Timed(Func<SpanningTree> build)
    {
        long started = Stopwatch.GetTimestamp();
        SpanningTree tree = build();
        return (tree, Stopwatch.GetElapsedTime(started));
    }

    private static IResult GetShortest(MeshNetwork network, MeshHealth health) =>
        BuildTree(network, health, DijkstraRoutes.Build);

    private static IResult BuildTree(
        MeshNetwork network,
        MeshHealth health,
        Func<MeshGraph, string, Func<string, bool>, SpanningTree> algorithm)
    {
        MeshView view = network.Snapshot(DateTimeOffset.UtcNow, health.IsDown);

        (SpanningTree tree, TimeSpan elapsed) = Timed(() => algorithm(view.Graph, MeshRadio.GatewayId, health.IsDown));

        return Results.Ok(SpanningTreeDto.FromTree(tree, elapsed));
    }

    // Both ways home side by side, the MST backbone route and the node's own cheapest route
    private static IResult GetRoute(string nodeIdOrMac, MeshNetwork network, SensorRegistry registry, MeshHealth health)
    {
        if (!TryResolveVertex(nodeIdOrMac, registry, out string nodeId))
        {
            return Results.Problem($"No node matches '{nodeIdOrMac}'.", statusCode: 404);
        }

        MeshView view = network.Snapshot(DateTimeOffset.UtcNow, health.IsDown);

        if (!view.Graph.HasVertex(nodeId))
        {
            return Results.Problem($"'{nodeId}' hasn't reported any neighbours yet.", statusCode: 404);
        }

        SpanningTree backbone = PrimMst.Build(view.Graph, MeshRadio.GatewayId, health.IsDown);
        SpanningTree shortest = DijkstraRoutes.Build(view.Graph, MeshRadio.GatewayId, health.IsDown);
        registry.TryGet(nodeId, out SensorRegistration? sensor);

        MeshNodeState state = health.IsDown(nodeId)
            ? MeshNodeState.Down
            : backbone.Contains(nodeId) ? MeshNodeState.Up : MeshNodeState.Orphaned;

        return Results.Ok(new RouteDto
        {
            NodeId = nodeId,
            Mac = sensor?.Mac.ToString(),
            State = state,
            Reachable = state == MeshNodeState.Up,
            Backbone = RoutePathDto.FromTree(backbone, nodeId),
            Shortest = RoutePathDto.FromTree(shortest, nodeId)
        });
    }

    private static MeshNodeDto ToNodeDto(
        string nodeId,
        MeshView view,
        BfsResult fromGateway,
        SensorRegistry registry,
        MeshHealth health)
    {
        registry.TryGet(nodeId, out SensorRegistration? sensor);
        MeshPosition position = view.Positions.GetValueOrDefault(nodeId);

        MeshNodeState state = health.IsDown(nodeId)
            ? MeshNodeState.Down
            : fromGateway.Reached(nodeId) ? MeshNodeState.Up : MeshNodeState.Orphaned;

        return new MeshNodeDto
        {
            NodeId = nodeId,
            Mac = sensor?.Mac.ToString(),
            Location = sensor?.Location.ToString(),
            Category = sensor?.Category,
            X = position.X,
            Y = position.Y,
            IsGateway = IsGateway(nodeId),
            State = state
        };
    }

    // Lets the operator type a node id, a MAC or GATEWAY
    private static bool TryResolveVertex(string nodeIdOrMac, SensorRegistry registry, out string nodeId)
    {
        nodeId = MeshRadio.GatewayId;

        if (IsGateway(nodeIdOrMac))
        {
            return true;
        }

        if (registry.TryResolve(nodeIdOrMac.Trim(), out SensorRegistration? sensor))
        {
            nodeId = sensor.NodeId;
            return true;
        }

        return false;
    }

    // Matches whole path segments so Zone1 doesn't also pick up Zone10
    private static bool IsInZone(string nodeId, string zone, SensorRegistry registry)
    {
        if (!registry.TryGet(nodeId, out SensorRegistration? sensor))
        {
            return false;
        }

        string location = sensor.Location.ToString();
        string prefix = zone.Trim().TrimEnd('/');

        return location.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            location.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string[]> Validate(NeighbourReportDto dto)
    {
        Dictionary<string, string[]> errors = [];

        if (!double.IsFinite(dto.X) || !double.IsFinite(dto.Y))
        {
            errors[nameof(dto.X)] = ["Position must be a real number."];
        }

        if (dto.Neighbours.Any(n => n.RssiDbm < MinRssiDbm || n.RssiDbm > MaxRssiDbm))
        {
            errors[nameof(dto.Neighbours)] = [$"RSSI must be between {MinRssiDbm} and {MaxRssiDbm} dBm."];
        }

        return errors;
    }

    private static bool IsGateway(string nodeId) =>
        string.Equals(nodeId, MeshRadio.GatewayId, StringComparison.OrdinalIgnoreCase);
}
