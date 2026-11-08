using SmartX.Core;

namespace SmartX.Api;

// Gives every newly registered node a day of made up history so the historical queries have real volume to work on
public sealed class HistorySeeder(
    SensorRegistry registry,
    TelemetryHistoryIndex history,
    IConfiguration configuration,
    ILogger<HistorySeeder> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly HashSet<string> _seeded = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        double hours = configuration.GetValue("History:SeedHours", 24.0);
        double stepSeconds = configuration.GetValue("History:SeedIntervalSeconds", 10.0);

        // Setting either to 0 turns seeding off, then only live readings go in
        if (hours <= 0 || stepSeconds <= 0)
        {
            logger.LogInformation("History seeding is off");
            return;
        }

        using PeriodicTimer timer = new(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Seed(TimeSpan.FromHours(hours), TimeSpan.FromSeconds(stepSeconds));
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
    }

    private void Seed(TimeSpan window, TimeSpan step)
    {
        IReadOnlyList<SensorRegistration> sensors = registry.All();

        // Forget deleted nodes so one registered again under the same id gets a fresh past
        _seeded.IntersectWith(sensors.Select(s => s.NodeId));

        foreach (SensorRegistration sensor in sensors.Where(s => !_seeded.Contains(s.NodeId)))
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // Stops where the live readings start so the two never overlap
            DateTimeOffset until = history.Span(sensor.NodeId)?.From ?? now;

            history.AddRange(sensor.NodeId, SyntheticHistory.For(sensor, now - window, until, step));
            _seeded.Add(sensor.NodeId);
        }

        if (sensors.Count > 0 && _seeded.Count == sensors.Count)
        {
            logger.LogDebug("History holds {Total} readings across {Nodes} nodes", history.TotalReadings, sensors.Count);
        }
    }
}
