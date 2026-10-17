using SmartX.Core.Contracts;

namespace SmartX.Core;

// Tracks what is currently wrong so the same problem is only ever reported once until it clears
public sealed class FaultSet
{
    private readonly HashSet<string> _disconnected = new(StringComparer.OrdinalIgnoreCase);

    // Severity is part of the key so a warning that gets worse still raises a critical
    private readonly HashSet<(string NodeId, AlertKind Kind, AlertSeverity Severity)> _activeErrors = [];
    private readonly Lock _gate = new();

    private long _suppressed;

    // Add returns false if it is already there, which is the whole point
    public bool MarkDisconnected(string nodeId)
    {
        lock (_gate)
        {
            bool added = _disconnected.Add(nodeId);

            if (!added)
            {
                _suppressed++;
            }

            return added;
        }
    }

    public bool MarkReconnected(string nodeId)
    {
        lock (_gate)
        {
            return _disconnected.Remove(nodeId);
        }
    }

    public bool TryRaise(AlertDto alert)
    {
        lock (_gate)
        {
            bool added = _activeErrors.Add((alert.NodeId, alert.Kind, alert.Severity));

            if (!added)
            {
                _suppressed++;
            }

            return added;
        }
    }

    // Called when a node reads normal again so its next problem gets reported fresh
    public bool ClearErrors(string nodeId)
    {
        lock (_gate)
        {
            return _activeErrors.RemoveWhere(e => SameNode(e.NodeId, nodeId)) > 0;
        }
    }

    public void Remove(string nodeId)
    {
        lock (_gate)
        {
            _disconnected.Remove(nodeId);
            _activeErrors.RemoveWhere(e => SameNode(e.NodeId, nodeId));
        }
    }

    public bool IsFaulty(string nodeId)
    {
        lock (_gate)
        {
            return _disconnected.Contains(nodeId) || _activeErrors.Any(e => SameNode(e.NodeId, nodeId));
        }
    }

    public FaultsDto Snapshot()
    {
        lock (_gate)
        {
            return new FaultsDto
            {
                Disconnected = [.. _disconnected.Order()],
                ActiveErrors = [.. _activeErrors
                    .OrderByDescending(e => e.Severity)
                    .ThenBy(e => e.NodeId)
                    .Select(e => new ActiveErrorDto(e.NodeId, e.Kind, e.Severity))],
                SuppressedDuplicates = _suppressed
            };
        }
    }

    private static bool SameNode(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
