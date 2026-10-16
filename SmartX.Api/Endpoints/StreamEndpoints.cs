using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

// Everything the real-time command stream page reads from
public static class StreamEndpoints
{
    public static RouteGroupBuilder MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/stream").WithTags("Stream");

        group.MapGet("/stats", GetStats);
        group.MapGet("/alerts", GetAlerts);

        return group;
    }

    private static IResult GetStats(TelemetryQueue queue, AlertQueue alerts)
    {
        (int depth, long processed, long critical, long warning) = alerts.Stats();

        QueueStatsDto stats = queue.Stats() with
        {
            AlertDepth = depth,
            AlertsProcessed = processed,
            CriticalAlerts = critical,
            WarningAlerts = warning
        };

        return Results.Ok(stats);
    }

    private static IResult GetAlerts(AlertQueue alerts, int limit = 50) =>
        Results.Ok(alerts.Recent(Math.Clamp(limit, 1, 200)));
}
