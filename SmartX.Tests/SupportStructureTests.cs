using SmartX.Core;

namespace SmartX.Tests;

public class SupportStructureTests
{
    [Fact]
    public void Heap_pops_in_order()
    {
        Random random = new(3);
        List<int> values = [.. Enumerable.Range(0, 1000).Select(_ => random.Next(-500, 500))];
        MinHeap<int> heap = new();

        foreach (int value in values)
        {
            heap.Push(value);
        }

        List<int> popped = [];

        while (heap.TryPop(out int value))
        {
            popped.Add(value);
        }

        Assert.Equal(values.Order(), popped);
    }

    [Fact]
    public void Heapify_gives_the_same_order_as_pushing()
    {
        int[] values = [9, 4, 7, 1, 8, 2, 2, 6];
        MinHeap<int> heap = new(values);

        Assert.Equal(1, heap.Peek());
        Assert.Equal(values.Order(), Enumerable.Range(0, values.Length).Select(_ => heap.Pop()));
    }

    [Fact]
    public void Empty_heap_throws_on_pop()
    {
        Assert.Throws<InvalidOperationException>(() => new MinHeap<int>().Pop());
    }

    [Fact]
    public void Union_find_spots_a_loop()
    {
        DisjointSet<string> set = new(["A", "B", "C", "D"]);

        Assert.True(set.Union("A", "B"));
        Assert.True(set.Union("B", "C"));

        // A and C are already joined through B, so this link would close a loop
        Assert.False(set.Union("A", "C"));
        Assert.True(set.Connected("A", "C"));
        Assert.False(set.Connected("A", "D"));
        Assert.Equal(2, set.SetCount);
    }

    [Fact]
    public void Graph_stores_each_link_once_at_both_ends()
    {
        MeshGraph graph = new();
        TestGraphs.Link(graph, "A", "B", -60);
        TestGraphs.Link(graph, "B", "A", -55);

        Assert.Equal(1, graph.EdgeCount);
        Assert.True(graph.TryGetLink("A", "B", out MeshLink? link));
        Assert.Equal(-55, link!.RssiDbm);

        graph.RemoveVertex("B");
        Assert.Empty(graph.Neighbours("A"));
    }

    [Fact]
    public void Weaker_signal_always_costs_more()
    {
        for (int rssi = MeshRadio.SensitivityDbm; rssi < MeshRadio.StrongDbm; rssi++)
        {
            Assert.True(MeshRadio.CostFor(rssi) >= MeshRadio.CostFor(rssi + 1));
        }

        // The bottom few dBm are capped at 20 tries so one terrible link can't blow the cost up
        Assert.Equal(MeshRadio.CostFor(-90), MeshRadio.CostFor(-88));
        Assert.True(MeshRadio.CostFor(-88) > MeshRadio.CostFor(-87));

        Assert.Equal(MeshRadio.BaseHopCost, MeshRadio.CostFor(-30));
    }

    [Fact]
    public void Registry_finds_a_node_by_mac_in_any_format()
    {
        SensorRegistry registry = new();
        registry.TryAdd(new SensorRegistration
        {
            NodeId = "SOIL-1",
            Mac = MacAddress.Parse("AA:BB:CC:DD:EE:01"),
            Location = default,
            Category = SensorCategory.Environmental,
            ExpectedInterval = TimeSpan.FromSeconds(1)
        });

        Assert.True(registry.TryResolve("aa-bb-cc-dd-ee-01", out SensorRegistration? byMac));
        Assert.Equal("SOIL-1", byMac.NodeId);
        Assert.True(registry.IsMacInUse(MacAddress.Parse("AABBCCDDEE01")));

        registry.Remove("SOIL-1");
        Assert.False(registry.IsMacInUse(MacAddress.Parse("AABBCCDDEE01")));
    }
}
