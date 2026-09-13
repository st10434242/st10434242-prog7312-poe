namespace SmartX.Core;

// Holds a recent window per node, fed from the staging buffer
public sealed class NodeTelemetryStore<T> where T : struct
{
    private readonly Lock _gate = new();
    private readonly TelemetryBatchBuffer<T> _incoming = new();
    private readonly Dictionary<string, TelemetryRingBuffer<TelemetryPacket<T>>> _byNode = [];
    private readonly int _windowSize;

    public NodeTelemetryStore(int windowSize = 60) => _windowSize = windowSize;

    public void Add(TelemetryPacket<T> packet)
    {
        lock (_gate)
        {
            _incoming.Add(packet);

            if (_incoming.IsFull)
            {
                Flush();
            }
        }
    }

    public IReadOnlyList<TelemetryPacket<T>> GetRecent(string nodeId)
    {
        lock (_gate)
        {
            // Flush first so a read sees everything that has arrived
            Flush();

            return _byNode.TryGetValue(nodeId, out TelemetryRingBuffer<TelemetryPacket<T>>? window)
                ? [.. window]
                : [];
        }
    }

    public IReadOnlyList<string> NodeIds()
    {
        lock (_gate)
        {
            Flush();
            return [.. _byNode.Keys];
        }
    }

    private void Flush()
    {
        foreach (TelemetryPacket<T> packet in _incoming.Drain())
        {
            if (!_byNode.TryGetValue(packet.NodeId, out TelemetryRingBuffer<TelemetryPacket<T>>? window))
            {
                window = new TelemetryRingBuffer<TelemetryPacket<T>>(_windowSize);
                _byNode[packet.NodeId] = window;
            }

            window.Add(packet);
        }
    }
}
