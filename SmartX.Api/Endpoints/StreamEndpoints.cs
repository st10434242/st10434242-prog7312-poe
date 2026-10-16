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
        group.MapGet("/timeline/{nodeId}", GetTimeline);
        group.MapGet("/events", GetEvents);

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

    // e.g. /api/stream/timeline/SOIL-Z1S1-01?from=2026-10-16T10:00:00Z&limit=200
    private static IResult GetTimeline(
        string nodeId,
        SensorRegistry registry,
        TimelineLog timeline,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 300)
    {
        if (!registry.TryGet(nodeId, out _))
        {
            return Results.NotFound();
        }

        if (from is not null && to is not null && from > to)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["from"] = ["from has to be before to."]
            });
        }

        return Results.Ok(timeline.Range(nodeId, from, to, Math.Clamp(limit, 1, 500)));
    }

    private static IResult GetEvents(TimelineLog timeline, string? nodeId = null, int limit = 100) =>
        Results.Ok(timeline.RecentEvents(Math.Clamp(limit, 1, 1000), nodeId));
}
