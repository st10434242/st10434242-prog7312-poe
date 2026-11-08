using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

public static class HistoryEndpoints
{
    private const int DefaultLimit = 2000;
    private const int MaxLimit = 20_000;
    private const int MaxBenchmarkSize = 1_000_000;
    private const int MaxBenchmarkSizes = 5;

    private const int DefaultWindowMinutes = 10;
    private const int MaxWindowMinutes = 24 * 60;

    private static readonly int[] DefaultBenchmarkSizes = [1_000, 10_000, 100_000, 1_000_000];

    // The benchmark eats CPU, so only one runs at a time
    private static readonly SemaphoreSlim BenchmarkGate = new(1, 1);

    public static RouteGroupBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/history").WithTags("History");

        group.MapGet("/", GetCoverage);
        group.MapGet("/benchmark", RunBenchmark);
        group.MapGet("/{nodeIdOrMac}", GetHistory);

        return group;
    }

    // Every node with how much history it has, for the picker on the dashboard
    private static IResult GetCoverage(SensorRegistry registry, TelemetryHistoryIndex history) =>
        Results.Ok(registry.All()
            .OrderBy(s => s.NodeId, StringComparer.OrdinalIgnoreCase)
            .Select(sensor =>
            {
                (DateTimeOffset From, DateTimeOffset To)? span = history.Span(sensor.NodeId);

                return new HistoryCoverageDto
                {
                    NodeId = sensor.NodeId,
                    Mac = sensor.Mac.ToString(),
                    Category = sensor.Category,
                    Readings = history.CountFor(sensor.NodeId),
                    OldestUtc = span?.From,
                    NewestUtc = span?.To
                };
            }));

    // Defaults to the last hour, from and to take any ISO time like 2026-11-08T14:00:00+02:00
    private static IResult GetHistory(
        string nodeIdOrMac,
        SensorRegistry registry,
        TelemetryHistoryIndex history,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? limit)
    {
        if (!registry.TryResolve(nodeIdOrMac.Trim(), out SensorRegistration? sensor))
        {
            return Results.Problem($"No node matches '{nodeIdOrMac}'.", statusCode: 404);
        }

        DateTimeOffset toUtc = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
        DateTimeOffset fromUtc = (from ?? toUtc.AddHours(-1)).ToUniversalTime();
        int maxPoints = limit ?? DefaultLimit;

        Dictionary<string, string[]> errors = [];

        if (fromUtc > toUtc)
        {
            errors[nameof(from)] = ["from has to be before to."];
        }

        if (maxPoints is < 1 or > MaxLimit)
        {
            errors[nameof(limit)] = [$"limit must be between 1 and {MaxLimit}."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        HistoryQueryResult result = history.Query(sensor.NodeId, fromUtc, toUtc);
        IReadOnlyList<HistoryReading> readings = result.Readings;

        return Results.Ok(new HistoryDto
        {
            NodeId = sensor.NodeId,
            Mac = sensor.Mac.ToString(),
            Category = sensor.Category,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Points = Thin(readings, maxPoints),
            Downsampled = readings.Count > maxPoints,
            Count = readings.Count,
            Min = readings.Count > 0 ? readings.Min(r => r.Value) : null,
            Max = readings.Count > 0 ? readings.Max(r => r.Value) : null,
            Average = readings.Count > 0 ? Math.Round(readings.Average(r => r.Value), 2) : null,
            NodesVisited = result.NodesVisited,
            TreeSize = result.TreeSize,
            TreeHeight = result.TreeHeight,
            QueryMicroseconds = Math.Round(result.Elapsed.TotalMicroseconds, 1)
        });
    }

    // sizes=1000,10000,100000 picks how big the test data gets, windowMinutes how wide each query is
    private static async Task<IResult> RunBenchmark(string? sizes, int? windowMinutes)
    {
        int[] parsed = DefaultBenchmarkSizes;
        int minutes = windowMinutes ?? DefaultWindowMinutes;

        if (minutes is < 1 or > MaxWindowMinutes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(windowMinutes)] = [$"windowMinutes must be between 1 and {MaxWindowMinutes}."]
            });
        }

        if (!string.IsNullOrWhiteSpace(sizes))
        {
            List<int> values = [];

            foreach (string part in sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(part, out int size) || size < 1 || size > MaxBenchmarkSize)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [nameof(sizes)] = [$"Each size must be a whole number from 1 to {MaxBenchmarkSize}."]
                    });
                }

                values.Add(size);
            }

            if (values.Count is 0 or > MaxBenchmarkSizes)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(sizes)] = [$"Give between 1 and {MaxBenchmarkSizes} sizes."]
                });
            }

            parsed = [.. values.Order()];
        }

        if (!await BenchmarkGate.WaitAsync(0))
        {
            return Results.Problem("A benchmark is already running, try again in a moment.", statusCode: 429);
        }

        try
        {
            // Off the request thread since a million inserts takes a second or two
            List<BenchmarkRowDto> rows = await Task.Run(() => parsed.Select(size => HistoryBenchmark.Run(size, TimeSpan.FromMinutes(minutes))).ToList());
            return Results.Ok(rows);
        }
        finally
        {
            BenchmarkGate.Release();
        }
    }

    // Takes every nth reading so a long window still charts quickly, the first and last always stay
    private static List<HistoryPointDto> Thin(IReadOnlyList<HistoryReading> readings, int maxPoints)
    {
        if (readings.Count <= maxPoints)
        {
            return [.. readings.Select(r => new HistoryPointDto(r.TimestampUtc, r.Value))];
        }

        List<HistoryPointDto> points = new(maxPoints);
        double stride = (double)(readings.Count - 1) / (maxPoints - 1);

        for (int i = 0; i < maxPoints; i++)
        {
            HistoryReading reading = readings[(int)Math.Round(i * stride)];
            points.Add(new HistoryPointDto(reading.TimestampUtc, reading.Value));
        }

        return points;
    }
}
