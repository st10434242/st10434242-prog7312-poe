using SmartX.Core;
using static SmartX.Tests.TestGraphs;

namespace SmartX.Tests;

public class SpanningTreeTests
{
    private static readonly double ChainTotal = Math.Round((4 * MeshRadio.CostFor(Cost10)) + MeshRadio.CostFor(Cost20), 2);

    [Fact]
    public void Prim_picks_the_cheapest_tree()
    {
        SpanningTree tree = PrimMst.Build(Diamond(), G, NoneDown);

        Assert.Equal(ChainTotal, tree.TotalCost);
        Assert.Equal(5, tree.Edges.Count);
        Assert.Empty(tree.Unspanned);
    }

    [Fact]
    public void Kruskal_picks_the_same_tree()
    {
        SpanningTree tree = KruskalMst.Build(Diamond(), G, NoneDown);

        Assert.Equal(ChainTotal, tree.TotalCost);
        Assert.Equal(5, tree.Edges.Count);
    }

    [Fact]
    public void Mst_route_follows_the_tree_to_the_gateway()
    {
        SpanningTree tree = PrimMst.Build(Diamond(), G, NoneDown);
        IReadOnlyList<RouteHop> route = tree.RouteToRoot("D");

        // Round the chain, not the direct G-D link, because the MST minimises the whole network
        Assert.Equal(["C", "B", "A", G], route.Select(h => h.To));
    }

    [Fact]
    public void Dijkstra_takes_the_direct_link_the_mst_skips()
    {
        SpanningTree tree = DijkstraRoutes.Build(Diamond(), G, NoneDown);
        IReadOnlyList<RouteHop> route = tree.RouteToRoot("D");

        RouteHop hop = Assert.Single(route);
        Assert.Equal(G, hop.To);
        Assert.Equal(MeshRadio.CostFor(Cost28), hop.Cost);
    }

    [Fact]
    public void Trees_heal_around_a_down_node()
    {
        foreach (SpanningTree tree in Trees(Diamond(), DownSet("B")))
        {
            Assert.False(tree.Contains("B"));
            Assert.True(tree.Contains("C"));
            Assert.DoesNotContain(tree.RouteToRoot("C"), h => h.To == "B" || h.From == "B");
        }
    }

    [Fact]
    public void Cut_off_nodes_end_up_unspanned()
    {
        foreach (SpanningTree tree in Trees(Diamond(), DownSet("D")))
        {
            Assert.Equal(["E"], tree.Unspanned);
            Assert.Empty(tree.RouteToRoot("E"));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Prim_and_kruskal_always_agree_on_random_meshes(int seed)
    {
        for (int round = 0; round < 40; round++)
        {
            (MeshGraph graph, HashSet<string> down) = TestGraphs.Random((seed * 1000) + round, 40);

            SpanningTree prim = PrimMst.Build(graph, G, down.Contains);
            SpanningTree kruskal = KruskalMst.Build(graph, G, down.Contains);

            // Ties can pick different links but a minimum spanning tree only has one total
            Assert.Equal(prim.TotalCost, kruskal.TotalCost, 2);
            Assert.Equal(prim.Edges.Count, kruskal.Edges.Count);
            Assert.Equal(prim.Unspanned, kruskal.Unspanned);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Dijkstra_never_costs_more_than_the_mst_route(int seed)
    {
        for (int round = 0; round < 40; round++)
        {
            (MeshGraph graph, HashSet<string> down) = TestGraphs.Random((seed * 1000) + round, 40);

            SpanningTree mst = PrimMst.Build(graph, G, down.Contains);
            SpanningTree shortest = DijkstraRoutes.Build(graph, G, down.Contains);

            foreach (string nodeId in graph.Vertices.Where(mst.Contains))
            {
                double viaMst = mst.RouteToRoot(nodeId).Sum(h => h.Cost);
                double viaDijkstra = shortest.RouteToRoot(nodeId).Sum(h => h.Cost);

                Assert.True(viaDijkstra <= viaMst + 1e-9, $"{nodeId}: {viaDijkstra} > {viaMst}");
            }
        }
    }

    [Fact]
    public void Every_route_ends_at_the_gateway()
    {
        (MeshGraph graph, HashSet<string> down) = TestGraphs.Random(99, 60);

        foreach (SpanningTree tree in Trees(graph, down.Contains))
        {
            foreach (string nodeId in graph.Vertices.Where(id => id != G && tree.Contains(id)))
            {
                Assert.Equal(G, tree.RouteToRoot(nodeId)[^1].To);
            }
        }
    }

    private static IEnumerable<SpanningTree> Trees(MeshGraph graph, Func<string, bool> isDown) =>
    [
        PrimMst.Build(graph, G, isDown),
        KruskalMst.Build(graph, G, isDown),
        DijkstraRoutes.Build(graph, G, isDown)
    ];
}
