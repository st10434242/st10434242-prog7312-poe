using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api;

// Watches the mesh and tells the dashboard when a node drops, gets cut off or comes back, so routes heal on screen
public sealed class MeshPublisher(
    MeshNetwork network,
    MeshHealth health,
    IHubContext<TelemetryHub> hub,
    ILogger<MeshPublisher> logger) : BackgroundService
{
    // Also works as the throttle, at most one push every two seconds no matter how much flaps
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private Dictionary<string, MeshNodeState> _lastStates = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _lastLinks = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CheckAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to check the mesh for changes");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        MeshView view = network.Snapshot(DateTimeOffset.UtcNow, health.IsDown);
        Dictionary<string, MeshNodeState> states = MeshReachability.States(view.Graph, health.IsDown);

        // Link ids are enough to tell if a link appeared or expired, RSSI wobble on its own doesn't matter
        HashSet<string> links = [.. view.Graph.Links().Select(LinkKey)];

        List<NodeStateChangeDto> changes = DiffStates(_lastStates, states);
        bool linksChanged = !links.SetEquals(_lastLinks);

        if (changes.Count == 0 && !linksChanged)
        {
            return;
        }

        _lastStates = states;
        _lastLinks = links;

        SpanningTree backbone = PrimMst.Build(view.Graph, MeshRadio.GatewayId, health.IsDown);

        MeshChangedDto changed = new()
        {
            Up = states.Values.Count(s => s == MeshNodeState.Up),
            Down = states.Values.Count(s => s == MeshNodeState.Down),
            Orphaned = states.Values.Count(s => s == MeshNodeState.Orphaned),
            Links = links.Count,
            BackboneCost = backbone.TotalCost,
            Changes = changes,
            LinksChanged = linksChanged
        };

        // Only state changes are worth a log line, links settle on their own while the mesh starts up
        foreach (NodeStateChangeDto change in changes.Where(c => c.From is not null))
        {
            logger.LogInformation("Mesh: {NodeId} went {From} -> {To}", change.NodeId, change.From, change.To);
        }

        await hub.Clients.All.SendAsync("MeshChanged", changed, cancellationToken);
    }

    private static List<NodeStateChangeDto> DiffStates(
        Dictionary<string, MeshNodeState> before,
        Dictionary<string, MeshNodeState> after)
    {
        List<NodeStateChangeDto> changes = [];

        foreach ((string nodeId, MeshNodeState state) in after)
        {
            MeshNodeState? previous = before.TryGetValue(nodeId, out MeshNodeState old) ? old : null;

            if (previous != state)
            {
                changes.Add(new NodeStateChangeDto { NodeId = nodeId, From = previous, To = state });
            }
        }

        // Anything that disappeared was removed from the registry
        foreach ((string nodeId, MeshNodeState state) in before)
        {
            if (!after.ContainsKey(nodeId))
            {
                changes.Add(new NodeStateChangeDto { NodeId = nodeId, From = state, To = null });
            }
        }

        return changes;
    }

    // Sorted so A-B and B-A give the same key
    private static string LinkKey(MeshLink link) =>
        string.Compare(link.From, link.To, StringComparison.OrdinalIgnoreCase) < 0
            ? $"{link.From}|{link.To}".ToUpperInvariant()
            : $"{link.To}|{link.From}".ToUpperInvariant();
}
