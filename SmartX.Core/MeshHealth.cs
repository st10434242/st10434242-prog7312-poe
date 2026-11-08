namespace SmartX.Core;

// Decides if a vertex is down, the graph itself only knows about links
public sealed class MeshHealth(NodeStateTracker tracker, FaultInjector faults)
{
    public bool IsDown(string nodeId)
    {
        // The gateway is the thing doing the judging, so it is always up
        if (string.Equals(nodeId, MeshRadio.GatewayId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Dimmed is only one missed publish, a node has to go fully stale to count as down
        return faults.IsSilenced(nodeId) || tracker.Get(nodeId) == LivenessState.Stale;
    }
}
