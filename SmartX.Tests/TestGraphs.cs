using SmartX.Core;

namespace SmartX.Tests;

// Small meshes where the right answer can be worked out by hand
public static class TestGraphs
{
    public const string G = MeshRadio.GatewayId;

    // RSSI values picked so the costs come out round: -50 = 10, -70 = 20, -80 = 40, -82 = 50
    public const int Cost10 = -50;
    public const int Cost20 = -70;
    public const int Cost40 = -80;
    public const int Cost50 = -82;

    // About 28.57, cheaper than going round the chain but dearer than any chain link
    public const int Cost28 = -76;

    // MST is G-A, A-B, B-C, C-D, D-E (total 60), so D gets sent round the chain for 50 instead of straight to G for 28.57
    public static MeshGraph Diamond()
    {
        MeshGraph graph = new();

        Link(graph, G, "A", Cost10);
        Link(graph, G, "B", Cost20);
        Link(graph, "A", "B", Cost10);
        Link(graph, "A", "C", Cost40);
        Link(graph, "B", "C", Cost20);
        Link(graph, "C", "D", Cost10);
        Link(graph, "B", "D", Cost50);
        Link(graph, G, "D", Cost28);
        Link(graph, "D", "E", Cost10);

        return graph;
    }

    // Random mesh for the property style tests, always seeded so a failure can be repeated
    public static (MeshGraph Graph, HashSet<string> Down) Random(int seed, int nodes)
    {
        Random random = new(seed);
        MeshGraph graph = new();
        List<string> ids = [G, .. Enumerable.Range(1, nodes - 1).Select(i => $"N{i}")];

        foreach (string id in ids)
        {
            graph.AddVertex(id);
        }

        for (int i = 0; i < nodes * 3; i++)
        {
            Link(graph, ids[random.Next(nodes)], ids[random.Next(nodes)], random.Next(-90, -45));
        }

        HashSet<string> down = [.. ids.Skip(1).Where(_ => random.NextDouble() < 0.15)];
        return (graph, down);
    }

    public static void Link(MeshGraph graph, string a, string b, int rssi) =>
        graph.AddOrUpdateLink(new MeshLink { From = a, To = b, RssiDbm = rssi });

    public static Func<string, bool> NoneDown => _ => false;

    public static Func<string, bool> DownSet(params string[] ids)
    {
        HashSet<string> set = new(ids, StringComparer.OrdinalIgnoreCase);
        return set.Contains;
    }
}
