using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

public static class TopologyEndpoints
{
    public static RouteGroupBuilder MapTopologyEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/topology").WithTags("Topology");

        group.MapGet("/", GetTopology);

        return group;
    }

    private static IResult GetTopology(SensorRegistry registry, TelemetryStore store)
    {
        // Rebuilt per request so it always reflects the current registrations
        DeploymentNode root = DeploymentNode.Build(registry.All());

        return Results.Ok(new TopologyDto
        {
            Root = DeploymentTree.Summarise(root, store),
            ValidationErrors = DeploymentTree.Validate(root)
        });
    }
}
