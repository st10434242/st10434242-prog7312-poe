using SmartX.Core.Contracts;

namespace SmartX.Core;

// A packet that tripped an alert, kept together so the reading still gets stored
public sealed record AlertWork(TelemetryPacketDto Packet, AlertDto Alert);

// Packets that tripped an alert skip the FIFO queue and wait here, worst severity first
public sealed class AlertQueue
{
    private const int RecentLimit = 200;

    // Severity is negated so the min-heap hands back critical first, the sequence keeps ties in arrival order
    private readonly PriorityQueue<AlertWork, (int Severity, long Sequence)> _pending = new();
    private readonly Queue<AlertDto> _recent = new();
    private readonly Lock _gate = new();
    private readonly IngestSignal _signal;

    private long _sequence;
    private long _processed;
    private long _critical;
    private long _warning;

    public AlertQueue(IngestSignal signal) => _signal = signal;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    // No capacity check on purpose, a critical alert should never be turned away
    public void Enqueue(TelemetryPacketDto packet, AlertDto alert)
    {
        lock (_gate)
        {
            _pending.Enqueue(new AlertWork(packet, alert), (-(int)alert.Severity, _sequence++));
        }

        _signal.Wake();
    }

    public bool TryDequeue(out AlertWork? work)
    {
        lock (_gate)
        {
            return _pending.TryDequeue(out work, out _);
        }
    }

    public void MarkProcessed(AlertDto alert)
    {
        lock (_gate)
        {
            _processed++;

            if (alert.Severity == AlertSeverity.Critical)
            {
                _critical++;
            }
            else
            {
                _warning++;
            }

            _recent.Enqueue(alert);

            // Only the latest ones are worth keeping for the page
            if (_recent.Count > RecentLimit)
            {
                _recent.Dequeue();
            }
        }
    }

    // Newest first since that is how the dashboard shows them
    public IReadOnlyList<AlertDto> Recent(int limit)
    {
        lock (_gate)
        {
            return [.. _recent.Reverse().Take(limit)];
        }
    }

    public (int Depth, long Processed, long Critical, long Warning) Stats()
    {
        lock (_gate)
        {
            return (_pending.Count, _processed, _critical, _warning);
        }
    }
}
