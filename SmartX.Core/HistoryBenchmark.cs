using System.Diagnostics;
using SmartX.Core.Contracts;

namespace SmartX.Core;

// Same data in an AVL tree and in a plain unsorted list, then the same time windows asked of both
public static class HistoryBenchmark
{
    // Enough windows that one slow run doesn't skew the average
    private const int QueriesPerSize = 50;

    public static BenchmarkRowDto Run(int size, TimeSpan window)
    {
        // Fixed seed so the numbers are repeatable between runs
        Random random = new(42);
        DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // One reading a second, shuffled so the list really is unorganised like raw packets arriving
        HistoryReading[] readings = new HistoryReading[size];

        for (int i = 0; i < size; i++)
        {
            readings[i] = new HistoryReading(start.AddSeconds(i), random.NextDouble() * 100);
        }

        random.Shuffle(readings);

        long buildStarted = Stopwatch.GetTimestamp();
        AvlTree<TelemetryKey, double> tree = new();

        for (int i = 0; i < readings.Length; i++)
        {
            tree.Insert(new TelemetryKey(readings[i].TimestampUtc, i), readings[i].Value);
        }

        TimeSpan buildTime = Stopwatch.GetElapsedTime(buildStarted);
        List<HistoryReading> list = [.. readings];

        // Windows picked up front so both structures answer exactly the same questions
        DateTimeOffset[] windowStarts = new DateTimeOffset[QueriesPerSize];
        double spanSeconds = Math.Max(size - window.TotalSeconds, 1);

        for (int q = 0; q < QueriesPerSize; q++)
        {
            windowStarts[q] = start.AddSeconds(random.NextDouble() * spanSeconds);
        }

        long avlTicks = 0;
        long listTicks = 0;
        long visited = 0;
        long found = 0;
        List<KeyValuePair<TelemetryKey, double>> treeResults = [];
        List<HistoryReading> listResults = [];

        // One untimed run of each first so neither side gets charged for the JIT compiling it
        tree.CollectRange(TelemetryKey.Earliest(start), TelemetryKey.Latest(start + window), treeResults);
        listResults.AddRange(list.Where(r => r.TimestampUtc >= start && r.TimestampUtc <= start + window));

        foreach (DateTimeOffset from in windowStarts)
        {
            DateTimeOffset to = from + window;

            treeResults.Clear();
            long started = Stopwatch.GetTimestamp();
            visited += tree.CollectRange(TelemetryKey.Earliest(from), TelemetryKey.Latest(to), treeResults);
            avlTicks += Stopwatch.GetTimestamp() - started;

            // The list has no order to use, so every single reading has to be checked
            listResults.Clear();
            started = Stopwatch.GetTimestamp();

            foreach (HistoryReading reading in list)
            {
                if (reading.TimestampUtc >= from && reading.TimestampUtc <= to)
                {
                    listResults.Add(reading);
                }
            }

            listTicks += Stopwatch.GetTimestamp() - started;
            found += treeResults.Count;
        }

        double avlMicroseconds = TicksToMicroseconds(avlTicks) / QueriesPerSize;
        double listMicroseconds = TicksToMicroseconds(listTicks) / QueriesPerSize;

        return new BenchmarkRowDto
        {
            Size = size,
            WindowMinutes = window.TotalMinutes,
            TreeHeight = tree.Height,
            BuildMilliseconds = Math.Round(buildTime.TotalMilliseconds, 1),
            AverageResults = found / QueriesPerSize,
            AvlMicroseconds = Math.Round(avlMicroseconds, 1),
            AvlNodesVisited = visited / QueriesPerSize,
            ListMicroseconds = Math.Round(listMicroseconds, 1),
            ListItemsScanned = size,
            Speedup = avlMicroseconds > 0 ? Math.Round(listMicroseconds / avlMicroseconds, 1) : 0
        };
    }

    private static double TicksToMicroseconds(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;
}
