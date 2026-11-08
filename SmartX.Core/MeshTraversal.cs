namespace SmartX.Core;

// What a breadth first search found, hop counts and the parent each node was reached from
public sealed class BfsResult
{
    private readonly Dictionary<string, int> _hops;
    private readonly Dictionary<string, string?> _parent;

    internal BfsResult(string start, List<string> visitOrder, Dictionary<string, int> hops, Dictionary<string, string?> parent)
    {
        Start = start;
        VisitOrder = visitOrder;
        _hops = hops;
        _parent = parent;
    }

    public string Start { get; }

    // Nearest first, since BFS finishes every node at one hop before moving to two
    public IReadOnlyList<string> VisitOrder { get; }

    public bool Reached(string nodeId) => _hops.ContainsKey(nodeId);

    public int? HopsTo(string nodeId) => _hops.TryGetValue(nodeId, out int hops) ? hops : null;

    public string? ParentOf(string nodeId) => _parent.GetValueOrDefault(nodeId);

    // Walks the parents back to the start then flips it, so the list reads start first
    public IReadOnlyList<string> PathTo(string nodeId)
    {
        if (!Reached(nodeId))
        {
            return [];
        }

        List<string> path = [];

        for (string? current = nodeId; current is not null; current = ParentOf(current))
        {
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}

public static class MeshTraversal
{
    // Down nodes can't forward anything, so the search never steps onto one
    public static BfsResult Bfs(MeshGraph graph, string start, Func<string, bool> isDown)
    {
        List<string> order = [];
        Dictionary<string, int> hops = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string?> parent = new(StringComparer.OrdinalIgnoreCase);

        if (!graph.HasVertex(start) || isDown(start))
        {
            return new BfsResult(start, order, hops, parent);
        }

        Queue<string> frontier = new();
        frontier.Enqueue(start);
        hops[start] = 0;
        parent[start] = null;

        while (frontier.Count > 0)
        {
            string current = frontier.Dequeue();
            order.Add(current);

            // Cheapest link first so when two parents are equally close the stronger link wins
            IEnumerable<MeshLink> links = graph.Neighbours(current)
                .OrderBy(link => link.Cost)
                .ThenBy(link => link.Other(current), StringComparer.OrdinalIgnoreCase);

            foreach (MeshLink link in links)
            {
                string next = link.Other(current);

                // hops doubles as the visited set
                if (hops.ContainsKey(next) || isDown(next))
                {
                    continue;
                }

                hops[next] = hops[current] + 1;
                parent[next] = current;
                frontier.Enqueue(next);
            }
        }

        return new BfsResult(start, order, hops, parent);
    }
}
