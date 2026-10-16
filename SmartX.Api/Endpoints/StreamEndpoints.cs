using SmartX.Core;

namespace SmartX.Api.Endpoints;

// Everything the real-time command stream page reads from
public static class StreamEndpoints
{
    public static RouteGroupBuilder MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/stream").WithTags("Stream");

        group.MapGet("/stats", GetStats);

        return group;
    }

    private static IResult GetStats(TelemetryQueue queue) => Results.Ok(queue.Stats());
}
