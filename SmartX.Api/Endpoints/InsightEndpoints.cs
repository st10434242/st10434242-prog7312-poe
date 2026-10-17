using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

// Suggested actions plus the activity feed the engine learns from
public static class InsightEndpoints
{
    public static RouteGroupBuilder MapInsightEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/insights").WithTags("Insights");

        group.MapGet("/", GetInsights);
        group.MapPost("/activity", RecordActivity);
        group.MapPost("/{id}/dismiss", Dismiss);
        group.MapGet("/patterns", GetPatterns);

        return group;
    }

    private static IResult GetInsights(InsightEngine engine, int limit = 8) =>
        Results.Ok(engine.Build(Math.Clamp(limit, 1, 20)));

    // Commands and alerts are recorded on the server already, so the dashboard only sends searches and views
    private static IResult RecordActivity(ActivityDto dto, SensorRegistry registry, PatternLearner learner)
    {
        if (dto.Kind is not (ActivityKind.Search or ActivityKind.View))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(dto.Kind)] = ["Only Search and View can be sent from the dashboard."]
            });
        }

        string term = dto.Term?.Trim() ?? string.Empty;

        if (term.Length is < 2 or > 64)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(dto.Term)] = ["Search terms have to be between 2 and 64 characters."]
            });
        }

        // If the term is an actual node id the engine can point at that device later
        string? nodeId = registry.TryGet(term, out SensorRegistration? registration) ? registration.NodeId : null;

        learner.Record(OperatorActivity.Search(term, nodeId, dto.Kind));
        return Results.NoContent();
    }

    private static IResult Dismiss(string id, InsightEngine engine)
    {
        engine.Dismiss(id);
        return Results.NoContent();
    }

    private static IResult GetPatterns(PatternLearner learner, int limit = 20) =>
        Results.Ok(learner.Patterns(Math.Clamp(limit, 1, 100)));
}
