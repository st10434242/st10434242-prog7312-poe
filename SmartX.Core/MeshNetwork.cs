using SmartX.Core.Contracts;

namespace SmartX.Core;

// Where a node said it is on the floor
public readonly record struct MeshPosition(double X, double Y);

// A frozen copy of the mesh for one request or one algorithm run
public sealed record MeshView(MeshGraph Graph, IReadOnlyDictionary<string, MeshPosition> Positions);

// The gateway's live picture of the mesh, built up from the neighbour reports the nodes send in
public sealed class MeshNetwork
{
    // Reports come every few seconds, so a link nobody has mentioned for this long has gone
    public static readonly TimeSpan LinkExpiry = TimeSpan.FromSeconds(15);

    private readonly Lock _gate = new();
    private readonly MeshGraph _graph = new();
    private readonly Dictionary<string, MeshPosition> _positions = new(StringComparer.OrdinalIgnoreCase);

    // Returns how many links were taken from the report
    public int ApplyReport(NeighbourReportDto report, Func<string, bool> canHear, DateTimeOffset now)
    {
        lock (_gate)
        {
            // A node only becomes a vertex once it reports, so every vertex has a position to draw at
            _graph.AddVertex(report.NodeId);
            _positions[report.NodeId] = new MeshPosition(report.X, report.Y);

            int applied = 0;

            foreach (NeighbourDto neighbour in report.Neighbours)
            {
                // Skip neighbours that haven't reported yet, they get linked on the next round
                if (!_graph.HasVertex(neighbour.NodeId) || !canHear(neighbour.NodeId))
                {
                    continue;
                }

                _graph.AddOrUpdateLink(new MeshLink
                {
                    From = report.NodeId,
                    To = neighbour.NodeId,
                    RssiDbm = neighbour.RssiDbm,
                    LastReportedUtc = now
                });

                applied++;
            }

            return applied;
        }
    }

    public void Remove(string nodeId)
    {
        lock (_gate)
        {
            _graph.RemoveVertex(nodeId);
            _positions.Remove(nodeId);
        }
    }

    public MeshView Snapshot(DateTimeOffset now, Func<string, bool> isDown)
    {
        lock (_gate)
        {
            PruneExpired(now, isDown);
            return new MeshView(_graph.Clone(), new Dictionary<string, MeshPosition>(_positions, StringComparer.OrdinalIgnoreCase));
        }
    }

    // A down node keeps its last known links so the gateway still knows who used to route through it
    private void PruneExpired(DateTimeOffset now, Func<string, bool> isDown)
    {
        List<MeshLink> expired = [.. _graph.Links()
            .Where(link => now - link.LastReportedUtc > LinkExpiry && !isDown(link.From) && !isDown(link.To))];

        foreach (MeshLink link in expired)
        {
            _graph.RemoveLink(link.From, link.To);
        }
    }
}
