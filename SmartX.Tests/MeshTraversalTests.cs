using SmartX.Core;
using SmartX.Core.Contracts;
using static SmartX.Tests.TestGraphs;

namespace SmartX.Tests;

public class MeshTraversalTests
{
    [Fact]
    public void Bfs_counts_hops_from_the_gateway()
    {
        BfsResult result = MeshTraversal.Bfs(Diamond(), G, NoneDown);

        Assert.Equal(0, result.HopsTo(G));
        Assert.Equal(1, result.HopsTo("A"));
        Assert.Equal(1, result.HopsTo("D"));
        Assert.Equal(2, result.HopsTo("C"));
        Assert.Equal(2, result.HopsTo("E"));
        Assert.Equal(G, result.VisitOrder[0]);
    }

    [Fact]
    public void Bfs_visits_nearest_first()
    {
        BfsResult result = MeshTraversal.Bfs(Diamond(), G, NoneDown);
        List<int> hops = [.. result.VisitOrder.Select(id => result.HopsTo(id)!.Value)];

        Assert.Equal(hops.Order(), hops);
    }

    [Fact]
    public void Bfs_never_steps_onto_a_down_node()
    {
        BfsResult result = MeshTraversal.Bfs(Diamond(), G, DownSet("D"));

        Assert.False(result.Reached("D"));

        // E only hangs off D, so with D down there is no way to it
        Assert.False(result.Reached("E"));
        Assert.True(result.Reached("C"));
        Assert.DoesNotContain("D", result.PathTo("C"));
    }

    [Fact]
    public void Path_runs_from_the_start_to_the_node()
    {
        BfsResult result = MeshTraversal.Bfs(Diamond(), G, NoneDown);

        Assert.Equal([G, "D", "E"], result.PathTo("E"));
    }

    [Fact]
    public void Starting_on_a_down_node_reaches_nothing()
    {
        BfsResult result = MeshTraversal.Bfs(Diamond(), "A", DownSet("A"));

        Assert.Empty(result.VisitOrder);
    }

    [Fact]
    public void Node_cut_off_by_a_failure_is_orphaned()
    {
        MeshGraph graph = Diamond();
        Func<string, bool> isDown = DownSet("D");
        BfsResult fromGateway = MeshTraversal.Bfs(graph, G, isDown);

        Assert.Equal(["E"], MeshReachability.Orphans(graph, fromGateway, isDown));
        Assert.Equal(MeshNodeState.Orphaned, MeshReachability.States(graph, isDown)["E"]);
        Assert.Equal(MeshNodeState.Down, MeshReachability.States(graph, isDown)["D"]);
    }

    [Fact]
    public void Reroutes_list_who_lost_their_route_and_where_they_go_now()
    {
        IReadOnlyList<Reroute> reroutes = MeshReachability.FindReroutes(Diamond(), DownSet("D"));

        Reroute e = Assert.Single(reroutes);
        Assert.Equal("E", e.NodeId);
        Assert.Equal(["D"], e.BlockedBy);
        Assert.Equal("D", e.OldNextHop);
        Assert.Null(e.NewNextHop);
    }

    [Fact]
    public void Reroute_finds_a_new_next_hop_when_there_is_one()
    {
        MeshGraph graph = Diamond();

        // Give E a second way out through C so losing D isn't the end of it
        Link(graph, "C", "E", Cost20);

        Reroute e = Assert.Single(MeshReachability.FindReroutes(graph, DownSet("D")), r => r.NodeId == "E");
        Assert.Equal("C", e.NewNextHop);
        Assert.Equal(3, e.NewHops);
    }
}
