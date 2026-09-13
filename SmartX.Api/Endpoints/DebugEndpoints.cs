using SmartX.Core;

namespace SmartX.Api.Endpoints;

// Lets a dropout be triggered on demand during a demo instead of waiting for one to happen
public static class DebugEndpoints
{
    public static RouteGroupBuilder MapDebugEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/debug").WithTags("Debug");

        group.MapPost("/silence/{nodeId}", Silence);
        group.MapDelete("/silence/{nodeId}", Restore);
        group.MapGet("/silence", GetSilenced);

        return group;
    }

    private static IResult Silence(string nodeId, SensorRegistry registry, FaultInjector faults)
    {
        if (!registry.TryGet(nodeId, out _))
        {
            return Results.NotFound();
        }

        faults.Silence(nodeId);
        return Results.NoContent();
    }

    private static IResult Restore(string nodeId, FaultInjector faults) =>
        faults.Restore(nodeId) ? Results.NoContent() : Results.NotFound();

    private static IResult GetSilenced(FaultInjector faults) => Results.Ok(faults.Silenced());
}
