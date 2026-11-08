using SmartX.Core;

namespace SmartX.Tests;

public class HistoryIndexTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Query_returns_only_the_window_in_time_order()
    {
        TelemetryHistoryIndex history = new();

        // Added out of order on purpose, the tree sorts them
        foreach (int minute in new[] { 30, 5, 125, 60, 90, 0, 150 })
        {
            history.Add("SOIL-1", Start.AddMinutes(minute), minute);
        }

        HistoryQueryResult result = history.Query("SOIL-1", Start.AddMinutes(30), Start.AddMinutes(120));

        Assert.Equal([30.0, 60.0, 90.0], result.Readings.Select(r => r.Value));
        Assert.Equal(7, result.TreeSize);
    }

    [Fact]
    public void Full_history_trims_the_oldest()
    {
        TelemetryHistoryIndex history = new(maxPerNode: 100);

        for (int i = 0; i < 250; i++)
        {
            history.Add("METER-1", Start.AddSeconds(i), i);
        }

        HistoryQueryResult result = history.Query("METER-1", Start, Start.AddDays(1));

        Assert.Equal(100, result.TreeSize);
        Assert.Equal(150, result.Readings[0].Value);
        Assert.Equal(249, result.Readings[^1].Value);
    }

    [Fact]
    public void Unknown_node_gives_an_empty_result()
    {
        TelemetryHistoryIndex history = new();

        HistoryQueryResult result = history.Query("NOPE", Start, Start.AddHours(1));

        Assert.Empty(result.Readings);
        Assert.Null(history.Span("NOPE"));
    }

    [Fact]
    public void Synthetic_history_covers_the_window_at_the_step()
    {
        SensorRegistration sensor = new()
        {
            NodeId = "SOIL-Z1S1-01",
            Mac = MacAddress.Parse("AA:BB:01:01:00:01"),
            Location = default,
            Category = SensorCategory.Environmental,
            ExpectedInterval = TimeSpan.FromSeconds(3)
        };

        List<HistoryReading> readings = [.. SyntheticHistory.For(sensor, Start, Start.AddHours(24), TimeSpan.FromSeconds(10))];

        Assert.Equal(8640, readings.Count);
        Assert.All(readings, r => Assert.InRange(r.Value, 0, 100));
    }
}
