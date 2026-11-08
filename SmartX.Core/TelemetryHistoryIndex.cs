using System.Collections.Concurrent;
using System.Diagnostics;

namespace SmartX.Core;

// One reading pulled back out of the history
public readonly record struct HistoryReading(DateTimeOffset TimestampUtc, double Value);

// What a range query found and how much of the tree it had to touch to find it
public sealed record HistoryQueryResult
{
    public required IReadOnlyList<HistoryReading> Readings { get; init; }

    public required int NodesVisited { get; init; }

    public required int TreeSize { get; init; }

    public required int TreeHeight { get; init; }

    public required TimeSpan Elapsed { get; init; }
}

// Long term history, one AVL tree per node keyed by timestamp so any time window is a quick range query
public sealed class TelemetryHistoryIndex
{
    // A day at one reading a second, after that the oldest gets trimmed
    public const int DefaultMaxPerNode = 86_400;

    private sealed class NodeHistory
    {
        public Lock Gate { get; } = new();

        public AvlTree<TelemetryKey, double> Tree { get; } = new();
    }

    private readonly ConcurrentDictionary<string, NodeHistory> _byNode = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxPerNode;

    // Shared across every node, only has to be unique so readings at the same instant don't collide
    private long _sequence;

    public TelemetryHistoryIndex(int maxPerNode = DefaultMaxPerNode) => _maxPerNode = maxPerNode;

    public void Add(string nodeId, DateTimeOffset timestampUtc, double value)
    {
        NodeHistory history = _byNode.GetOrAdd(nodeId, _ => new NodeHistory());
        TelemetryKey key = new(timestampUtc, Interlocked.Increment(ref _sequence));

        lock (history.Gate)
        {
            history.Tree.Insert(key, value);

            // Oldest is always the leftmost node, so trimming is just a delete-min
            while (history.Tree.Count > _maxPerNode)
            {
                history.Tree.RemoveMin();
            }
        }
    }

    // Bulk insert for the seeder, one lock for the whole batch
    public void AddRange(string nodeId, IEnumerable<HistoryReading> readings)
    {
        NodeHistory history = _byNode.GetOrAdd(nodeId, _ => new NodeHistory());

        lock (history.Gate)
        {
            foreach (HistoryReading reading in readings)
            {
                history.Tree.Insert(new TelemetryKey(reading.TimestampUtc, Interlocked.Increment(ref _sequence)), reading.Value);
            }

            while (history.Tree.Count > _maxPerNode)
            {
                history.Tree.RemoveMin();
            }
        }
    }

    public HistoryQueryResult Query(string nodeId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (!_byNode.TryGetValue(nodeId, out NodeHistory? history))
        {
            return new HistoryQueryResult { Readings = [], NodesVisited = 0, TreeSize = 0, TreeHeight = 0, Elapsed = TimeSpan.Zero };
        }

        List<KeyValuePair<TelemetryKey, double>> found = [];

        lock (history.Gate)
        {
            long started = Stopwatch.GetTimestamp();
            int visited = history.Tree.CollectRange(TelemetryKey.Earliest(fromUtc), TelemetryKey.Latest(toUtc), found);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

            return new HistoryQueryResult
            {
                Readings = [.. found.Select(pair => new HistoryReading(pair.Key.TimestampUtc, pair.Value))],
                NodesVisited = visited,
                TreeSize = history.Tree.Count,
                TreeHeight = history.Tree.Height,
                Elapsed = elapsed
            };
        }
    }

    // Oldest and newest reading held for a node, so the dashboard knows what range it can ask for
    public (DateTimeOffset From, DateTimeOffset To)? Span(string nodeId)
    {
        if (!_byNode.TryGetValue(nodeId, out NodeHistory? history))
        {
            return null;
        }

        lock (history.Gate)
        {
            KeyValuePair<TelemetryKey, double>? min = history.Tree.Min();
            KeyValuePair<TelemetryKey, double>? max = history.Tree.Max();

            return min is null || max is null ? null : (min.Value.Key.TimestampUtc, max.Value.Key.TimestampUtc);
        }
    }

    public int CountFor(string nodeId)
    {
        if (!_byNode.TryGetValue(nodeId, out NodeHistory? history))
        {
            return 0;
        }

        lock (history.Gate)
        {
            return history.Tree.Count;
        }
    }

    public long TotalReadings => _byNode.Values.Sum(h => { lock (h.Gate) { return (long)h.Tree.Count; } });

    public void Remove(string nodeId) => _byNode.TryRemove(nodeId, out _);
}
