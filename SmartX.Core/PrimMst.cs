namespace SmartX.Core;

// Grows the tree out from the gateway, always taking the cheapest link that reaches a node not in it yet
public static class PrimMst
{
    public const string Name = "Prim";

    // A link waiting in the heap and the node it would bring into the tree
    private readonly record struct Candidate(string To, MeshLink Link);

    // Cost first, then ids so equal costs always come out in the same order
    private static readonly IComparer<Candidate> ByCost = Comparer<Candidate>.Create((a, b) =>
    {
        int byCost = a.Link.Cost.CompareTo(b.Link.Cost);
        return byCost != 0 ? byCost : StringComparer.OrdinalIgnoreCase.Compare(a.To, b.To);
    });

    public static SpanningTree Build(MeshGraph graph, string root, Func<string, bool> isDown)
    {
        List<string> live = [.. graph.Vertices.Where(v => !isDown(v))];

        if (!graph.HasVertex(root) || isDown(root))
        {
            return SpanningTree.FromEdges(root, Name, [], live);
        }

        HashSet<string> inTree = new(StringComparer.OrdinalIgnoreCase);
        List<MeshLink> treeEdges = [];
        MinHeap<Candidate> frontier = new(ByCost);

        AddToTree(root);

        while (frontier.TryPop(out Candidate cheapest))
        {
            // Lazy deletion, a cheaper link already brought this node in so this entry is just stale
            if (inTree.Contains(cheapest.To))
            {
                continue;
            }

            treeEdges.Add(cheapest.Link);
            AddToTree(cheapest.To);
        }

        return SpanningTree.FromEdges(root, Name, treeEdges, live);

        void AddToTree(string nodeId)
        {
            inTree.Add(nodeId);

            foreach (MeshLink link in graph.Neighbours(nodeId))
            {
                string next = link.Other(nodeId);

                // A down node can't relay so it never joins the tree
                if (!inTree.Contains(next) && !isDown(next))
                {
                    frontier.Push(new Candidate(next, link));
                }
            }
        }
    }
}
