using SmartX.Core.Contracts;

namespace SmartX.Core;

// Sensor history ordered by timestamp, so even packets that arrive late end up in the right spot on the graph
public sealed class TimelineLog
{
    private readonly Dictionary<string, SortedDictionary<DateTimeOffset, double>> _byNode = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedDictionary<DateTimeOffset, TimelineEventDto> _events = new();
    private readonly Lock _gate = new();
    private readonly int _pointsPerNode;
    private readonly int _maxEvents;

    public TimelineLog(int pointsPerNode = 500, int maxEvents = 1000)
    {
        _pointsPerNode = pointsPerNode;
        _maxEvents = maxEvents;
    }

    public void RecordReading(string nodeId, DateTimeOffset timestamp, double value)
    {
        lock (_gate)
        {
            if (!_byNode.TryGetValue(nodeId, out SortedDictionary<DateTimeOffset, double>? points))
            {
                points = new SortedDictionary<DateTimeOffset, double>();
                _byNode[nodeId] = points;
            }

            Insert(points, timestamp, value, _pointsPerNode);
        }
    }

    public void LogEvent(TimelineEventDto timelineEvent)
    {
        lock (_gate)
        {
            Insert(_events, timelineEvent.TimestampUtc, timelineEvent, _maxEvents);
        }
    }

    public void Remove(string nodeId)
    {
        lock (_gate)
        {
            _byNode.Remove(nodeId);
        }
    }

    // Keys are already sorted so we can stop as soon as we pass the end of the range
    public NodeTimelineDto Range(string nodeId, DateTimeOffset? from, DateTimeOffset? to, int limit)
    {
        lock (_gate)
        {
            List<TimelinePointDto> points = [];

            if (_byNode.TryGetValue(nodeId, out SortedDictionary<DateTimeOffset, double>? log))
            {
                foreach (KeyValuePair<DateTimeOffset, double> entry in log)
                {
                    if (from is not null && entry.Key < from)
                    {
                        continue;
                    }

                    if (to is not null && entry.Key > to)
                    {
                        break;
                    }

                    points.Add(new TimelinePointDto(entry.Key, entry.Value));
                }
            }

            // If there are too many for the graph keep the newest ones
            if (points.Count > limit)
            {
                points = points.GetRange(points.Count - limit, limit);
            }

            return new NodeTimelineDto
            {
                NodeId = nodeId,
                Count = points.Count,
                Points = points
            };
        }
    }

    // Newest first for the history feed
    public IReadOnlyList<TimelineEventDto> RecentEvents(int limit, string? nodeId = null)
    {
        lock (_gate)
        {
            return [.. _events.Values
                .Reverse()
                .Where(e => nodeId is null || string.Equals(e.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                .Take(limit)];
        }
    }

    private static void Insert<T>(SortedDictionary<DateTimeOffset, T> log, DateTimeOffset timestamp, T value, int cap)
    {
        // Keys have to be unique so two things at the exact same moment get nudged a tick apart
        while (log.ContainsKey(timestamp))
        {
            timestamp = timestamp.AddTicks(1);
        }

        log.Add(timestamp, value);

        // The first key is always the oldest one since it is sorted
        if (log.Count > cap)
        {
            log.Remove(log.Keys.First());
        }
    }
}
