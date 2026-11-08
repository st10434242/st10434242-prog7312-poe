namespace SmartX.Core;

// Works from the cheapest link up across the whole network, keeping a link unless it would make a loop
public static class KruskalMst
{
    public const string Name = "Kruskal";

    // Cost first, then the ids so ties always come out the same way
    private static readonly IComparer<MeshLink> ByCost = Comparer<MeshLink>.Create((a, b) =>
    {
        int byCost = a.Cost.CompareTo(b.Cost);

        if (byCost != 0)
        {
            return byCost;
        }

        int byFrom = StringComparer.OrdinalIgnoreCase.Compare(a.From, b.From);
        return byFrom != 0 ? byFrom : StringComparer.OrdinalIgnoreCase.Compare(a.To, b.To);
    });

    public static SpanningTree Build(MeshGraph graph, string root, Func<string, bool> isDown)
    {
        List<string> live = [.. graph.Vertices.Where(v => !isDown(v))];

        if (!graph.HasVertex(root) || isDown(root))
        {
            return SpanningTree.FromEdges(root, Name, [], live);
        }

        // Heapified in one go instead of sorting, and links touching a down node never go in
        MinHeap<MeshLink> links = new(graph.Links().Where(l => !isDown(l.From) && !isDown(l.To)), ByCost);
        DisjointSet<string> joined = new(live, StringComparer.OrdinalIgnoreCase);
        List<MeshLink> treeEdges = [];

        // A tree over n nodes has n - 1 links, so stop as soon as everything is in one set
        while (joined.SetCount > 1 && links.TryPop(out MeshLink? cheapest))
        {
            if (joined.Union(cheapest.From, cheapest.To))
            {
                treeEdges.Add(cheapest);
            }
        }

        // Kruskal builds a forest if part of the mesh is cut off, FromEdges keeps only the tree the gateway is in
        return SpanningTree.FromEdges(root, Name, treeEdges, live);
    }
}
