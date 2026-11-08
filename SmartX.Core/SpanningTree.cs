namespace SmartX.Core;

// One hop on the way to the gateway
public readonly record struct RouteHop(string From, string To, int RssiDbm, double Cost);

// The routing tree an MST algorithm picked, rooted at the gateway so every node has one way home
public sealed class SpanningTree
{
    // Each node's link towards the root, following these is the route
    private readonly Dictionary<string, MeshLink> _uplink;
    private readonly Dictionary<string, string> _parent;

    private SpanningTree(
        string root,
        string algorithm,
        List<MeshLink> edges,
        Dictionary<string, MeshLink> uplink,
        Dictionary<string, string> parent,
        List<string> unspanned)
    {
        Root = root;
        Algorithm = algorithm;
        Edges = edges;
        _uplink = uplink;
        _parent = parent;
        Unspanned = unspanned;
        TotalCost = Math.Round(edges.Sum(e => e.Cost), 2);
    }

    public string Root { get; }

    public string Algorithm { get; }

    public IReadOnlyList<MeshLink> Edges { get; }

    public double TotalCost { get; }

    // Live nodes the tree couldn't get to, the orphans
    public IReadOnlyList<string> Unspanned { get; }

    public int SpannedCount => Edges.Count + 1;

    public bool Contains(string nodeId) =>
        string.Equals(nodeId, Root, StringComparison.OrdinalIgnoreCase) || _uplink.ContainsKey(nodeId);

    public string? ParentOf(string nodeId) => _parent.GetValueOrDefault(nodeId);

    // Climbs the uplinks until it hits the root, there are no cycles in a tree so it always ends
    public IReadOnlyList<RouteHop> RouteToRoot(string nodeId)
    {
        List<RouteHop> hops = [];
        string current = nodeId;

        while (_uplink.TryGetValue(current, out MeshLink? link))
        {
            string next = _parent[current];
            hops.Add(new RouteHop(current, next, link.RssiDbm, link.Cost));
            current = next;
        }

        return hops;
    }

    // Both Prim and Kruskal just hand over the edges they picked, this works out the parents from the root down
    public static SpanningTree FromEdges(string root, string algorithm, IEnumerable<MeshLink> edges, IEnumerable<string> liveVertices)
    {
        Dictionary<string, List<MeshLink>> adjacency = new(StringComparer.OrdinalIgnoreCase);

        foreach (MeshLink edge in edges)
        {
            adjacency.TryAdd(edge.From, []);
            adjacency.TryAdd(edge.To, []);
            adjacency[edge.From].Add(edge);
            adjacency[edge.To].Add(edge);
        }

        Dictionary<string, MeshLink> uplink = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> parent = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { root };
        List<MeshLink> treeEdges = [];
        Queue<string> frontier = new();
        frontier.Enqueue(root);

        // Only edges hanging off the root count, Kruskal can leave separate little trees for cut off nodes
        while (frontier.Count > 0)
        {
            string current = frontier.Dequeue();

            foreach (MeshLink link in adjacency.GetValueOrDefault(current) ?? [])
            {
                string child = link.Other(current);

                if (!seen.Add(child))
                {
                    continue;
                }

                uplink[child] = link;
                parent[child] = current;
                treeEdges.Add(link);
                frontier.Enqueue(child);
            }
        }

        List<string> unspanned = [.. liveVertices.Where(v => !seen.Contains(v)).Order(StringComparer.OrdinalIgnoreCase)];

        return new SpanningTree(root, algorithm, treeEdges, uplink, parent, unspanned);
    }
}
