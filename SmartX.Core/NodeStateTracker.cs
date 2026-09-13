using System.Collections.Concurrent;

namespace SmartX.Core;

// Remembers each node's last known liveness so the sweeper only announces actual changes
public sealed class NodeStateTracker
{
    private readonly ConcurrentDictionary<string, LivenessState> _liveness =
        new(StringComparer.OrdinalIgnoreCase);

    public bool Update(string nodeId, LivenessState state)
    {
        bool changed = !_liveness.TryGetValue(nodeId, out LivenessState previous) || previous != state;
        _liveness[nodeId] = state;
        return changed;
    }

    public LivenessState Get(string nodeId) =>
        _liveness.TryGetValue(nodeId, out LivenessState state) ? state : LivenessState.Stale;

    public void Remove(string nodeId) => _liveness.TryRemove(nodeId, out _);
}
