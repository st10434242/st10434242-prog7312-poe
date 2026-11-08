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
        MeshView view = network.Snapshot(DateTimeOffset.UtcNow);

        List<MeshNodeDto> nodes = [.. view.Graph.Vertices
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(nodeId => ToNodeDto(nodeId, view, registry, health))];

        List<MeshEdgeDto> edges = [.. view.Graph.Links().Select(MeshEdgeDto.FromLink)];

        return Results.Ok(new MeshSnapshotDto { Nodes = nodes, Edges = edges });
    }

    private static MeshNodeDto ToNodeDto(string nodeId, MeshView view, SensorRegistry registry, MeshHealth health)
    {
        registry.TryGet(nodeId, out SensorRegistration? sensor);
        MeshPosition position = view.Positions.GetValueOrDefault(nodeId);

        return new MeshNodeDto
        {
            NodeId = nodeId,
            Mac = sensor?.Mac.ToString(),
            Location = sensor?.Location.ToString(),
            Category = sensor?.Category,
            X = position.X,
            Y = position.Y,
            IsGateway = IsGateway(nodeId),
            State = health.IsDown(nodeId) ? MeshNodeState.Down : MeshNodeState.Up
        };
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
