namespace SmartX.Core;

// Union-find, keeps track of which nodes are already joined so Kruskal can spot a link that would make a loop
public sealed class DisjointSet<T> where T : notnull
{
    private readonly Dictionary<T, T> _parent;
    private readonly Dictionary<T, int> _rank;

    public DisjointSet(IEnumerable<T> items, IEqualityComparer<T>? comparer = null)
    {
        _parent = new Dictionary<T, T>(comparer);
        _rank = new Dictionary<T, int>(comparer);

        // Everything starts in its own set, pointing at itself
        foreach (T item in items)
        {
            _parent[item] = item;
            _rank[item] = 0;
        }

        SetCount = _parent.Count;
    }

    public int SetCount { get; private set; }

    // Path compression, every node on the way up gets pointed straight at the root for next time
    public T Find(T item)
    {
        T parent = _parent[item];

        if (_parent.Comparer.Equals(parent, item))
        {
            return item;
        }

        T root = Find(parent);
        _parent[item] = root;
        return root;
    }

    // Returns false if they were already in the same set, which for Kruskal means the link would close a loop
    public bool Union(T a, T b)
    {
        T rootA = Find(a);
        T rootB = Find(b);

        if (_parent.Comparer.Equals(rootA, rootB))
        {
            return false;
        }

        // Union by rank, the shorter tree goes under the taller one so the trees stay flat
        if (_rank[rootA] < _rank[rootB])
        {
            (rootA, rootB) = (rootB, rootA);
        }

        _parent[rootB] = rootA;

        if (_rank[rootA] == _rank[rootB])
        {
            _rank[rootA]++;
        }

        SetCount--;
        return true;
    }

    public bool Connected(T a, T b) => _parent.Comparer.Equals(Find(a), Find(b));
}
