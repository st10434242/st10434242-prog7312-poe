namespace SmartX.Core;

// Cheapest path from the gateway to every node, the MST minimises the whole network but this minimises each node's own trip
public static class DijkstraRoutes
{
    public const string Name = "Dijkstra";

    // Best known distance to a node and the link it would arrive on
    private readonly record struct Candidate(double Distance, string NodeId, MeshLink? Via);

    private static readonly IComparer<Candidate> ByDistance = Comparer<Candidate>.Create((a, b) =>
    {
        int byDistance = a.Distance.CompareTo(b.Distance);
        return byDistance != 0 ? byDistance : StringComparer.OrdinalIgnoreCase.Compare(a.NodeId, b.NodeId);
    });

    public static SpanningTree Build(MeshGraph graph, string root, Func<string, bool> isDown)
    {
        List<string> live = [.. graph.Vertices.Where(v => !isDown(v))];

        if (!graph.HasVertex(root) || isDown(root))
        {
            return SpanningTree.FromEdges(root, Name, [], live);
        }

        Dictionary<string, double> distance = new(StringComparer.OrdinalIgnoreCase) { [root] = 0 };
        HashSet<string> settled = new(StringComparer.OrdinalIgnoreCase);
        List<MeshLink> treeEdges = [];
        MinHeap<Candidate> frontier = new(ByDistance);

        frontier.Push(new Candidate(0, root, null));

        while (frontier.TryPop(out Candidate closest))
        {
            // Same lazy deletion as Prim, a shorter way here was already found so skip the old entry
            if (!settled.Add(closest.NodeId))
            {
                continue;
            }

            if (closest.Via is not null)
            {
                treeEdges.Add(closest.Via);
            }

            foreach (MeshLink link in graph.Neighbours(closest.NodeId))
            {
                string next = link.Other(closest.NodeId);

                if (settled.Contains(next) || isDown(next))
                {
                    continue;
                }

                double throughHere = closest.Distance + link.Cost;

                // Relax the edge, only worth pushing if it beats the best way in so far
                if (throughHere < distance.GetValueOrDefault(next, double.PositiveInfinity))
                {
                    distance[next] = throughHere;
                    frontier.Push(new Candidate(throughHere, next, link));
                }
            }
        }

        return SpanningTree.FromEdges(root, Name, treeEdges, live);
    }
}
