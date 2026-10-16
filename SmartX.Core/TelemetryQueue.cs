using SmartX.Core.Contracts;

namespace SmartX.Core;

// FIFO buffer between the endpoints and the processor so a request never waits on storage or SignalR
public sealed class TelemetryQueue
{
    private readonly Queue<TelemetryPacketDto> _packets = new();
    private readonly Lock _gate = new();
    private readonly IngestSignal _signal;
    private readonly int _capacity;

    private long _enqueued;
    private long _processed;
    private long _rejected;

    // Used to work out packets per second without keeping every timestamp
    private long _currentSecond;
    private int _processedThisSecond;
    private int _lastSecondRate;

    public TelemetryQueue(IngestSignal signal, int capacity = 10_000)
    {
        _signal = signal;
        _capacity = capacity;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _packets.Count;
            }
        }
    }

    public bool TryEnqueue(TelemetryPacketDto packet)
    {
        lock (_gate)
        {
            // Better to turn a device away than run the gateway out of memory
            if (_packets.Count >= _capacity)
            {
                _rejected++;
                return false;
            }

            _packets.Enqueue(packet);
            _enqueued++;
        }

        _signal.Wake();
        return true;
    }

    public bool TryDequeue(out TelemetryPacketDto? packet)
    {
        lock (_gate)
        {
            return _packets.TryDequeue(out packet);
        }
    }

    public void MarkProcessed()
    {
        lock (_gate)
        {
            _processed++;

            long second = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (second != _currentSecond)
            {
                // If a whole second went by with nothing processed the rate is really zero
                _lastSecondRate = second == _currentSecond + 1 ? _processedThisSecond : 0;
                _currentSecond = second;
                _processedThisSecond = 0;
            }

            _processedThisSecond++;
        }
    }

    public QueueStatsDto Stats()
    {
        lock (_gate)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int rate = now - _currentSecond <= 1 ? _lastSecondRate : 0;

            return new QueueStatsDto
            {
                Depth = _packets.Count,
                Capacity = _capacity,
                Enqueued = _enqueued,
                Processed = _processed,
                Rejected = _rejected,
                PacketsPerSecond = rate
            };
        }
    }
}
