using System.Collections.Concurrent;

namespace SmartX.Core;

// Lets a node be silenced on demand so a dropout can be shown during a demo. The simulator keeps
// publishing, the gateway just drops what it sends.
public sealed class FaultInjector
{
    private readonly ConcurrentDictionary<string, byte> _silenced =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsSilenced(string nodeId) => _silenced.ContainsKey(nodeId);

    public void Silence(string nodeId) => _silenced[nodeId] = 0;

    public bool Restore(string nodeId) => _silenced.TryRemove(nodeId, out _);

    public IReadOnlyList<string> Silenced() => [.. _silenced.Keys];
}
