using SmartX.Core;

namespace SmartX.Tests;

public class AvlTreeTests
{
    [Fact]
    public void Sorted_inserts_stay_balanced()
    {
        AvlTree<int, int> tree = new();

        // Sorted order is what turns a plain BST into a linked list
        for (int i = 0; i < 100_000; i++)
        {
            tree.Insert(i, i);
        }

        Assert.True(tree.IsValid());
        Assert.Equal(100_000, tree.Count);
        Assert.True(tree.Height <= 1.45 * Math.Log2(tree.Count + 2), $"height {tree.Height} is too tall");
        Assert.True(tree.Rotations > 0);
    }

    [Fact]
    public void Random_inserts_and_removes_match_a_sorted_dictionary()
    {
        Random random = new(5);
        AvlTree<int, int> tree = new();
        SortedDictionary<int, int> expected = [];

        for (int i = 0; i < 20_000; i++)
        {
            int key = random.Next(2_000);

            if (random.Next(3) == 0)
            {
                Assert.Equal(expected.Remove(key), tree.Remove(key));
            }
            else
            {
                Assert.Equal(!expected.ContainsKey(key), tree.Insert(key, i));
                expected[key] = i;
            }
        }

        Assert.True(tree.IsValid());
        Assert.Equal(expected, tree.InOrder());
    }

    [Fact]
    public void Range_includes_both_ends()
    {
        AvlTree<int, string> tree = new();

        for (int i = 0; i < 100; i++)
        {
            tree.Insert(i, $"v{i}");
        }

        List<KeyValuePair<int, string>> found = tree.Range(10, 20);

        Assert.Equal(11, found.Count);
        Assert.Equal(10, found[0].Key);
        Assert.Equal(20, found[^1].Key);
    }

    [Fact]
    public void Range_only_visits_a_path_plus_the_results()
    {
        AvlTree<int, int> tree = new();

        for (int i = 0; i < 100_000; i++)
        {
            tree.Insert(i, i);
        }

        List<KeyValuePair<int, int>> found = [];
        int visited = tree.CollectRange(50_000, 50_009, found);

        Assert.Equal(10, found.Count);

        // O(log n + k), two root to leaf paths at most on top of the results
        Assert.True(visited <= found.Count + (2 * tree.Height), $"visited {visited}");
    }

    [Fact]
    public void Range_with_from_after_to_is_empty()
    {
        AvlTree<int, int> tree = new();
        tree.Insert(1, 1);

        Assert.Empty(tree.Range(5, 1));
    }

    [Fact]
    public void Remove_min_takes_the_oldest()
    {
        AvlTree<int, int> tree = new();

        foreach (int key in new[] { 5, 3, 8, 1, 4 })
        {
            tree.Insert(key, key);
        }

        Assert.True(tree.RemoveMin());
        Assert.Equal(3, tree.Min()!.Value.Key);
        Assert.Equal(8, tree.Max()!.Value.Key);
        Assert.True(tree.IsValid());
    }

    [Fact]
    public void Inserting_an_existing_key_replaces_the_value()
    {
        AvlTree<int, string> tree = new();

        Assert.True(tree.Insert(1, "old"));
        Assert.False(tree.Insert(1, "new"));

        Assert.Equal(1, tree.Count);
        Assert.True(tree.TryGetValue(1, out string? value));
        Assert.Equal("new", value);
    }

    [Fact]
    public void Same_timestamp_readings_are_both_kept()
    {
        AvlTree<TelemetryKey, double> tree = new();
        DateTimeOffset at = DateTimeOffset.UtcNow;

        tree.Insert(new TelemetryKey(at, 1), 10);
        tree.Insert(new TelemetryKey(at, 2), 20);

        List<KeyValuePair<TelemetryKey, double>> found = tree.Range(TelemetryKey.Earliest(at), TelemetryKey.Latest(at));

        Assert.Equal([10.0, 20.0], found.Select(p => p.Value));
    }
}
