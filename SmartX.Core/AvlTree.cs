namespace SmartX.Core;

// Self balancing binary search tree, the two sides of any node never differ in height by more than one
public sealed class AvlTree<TKey, TValue>
{
    private sealed class Node(TKey key, TValue value)
    {
        public TKey Key { get; } = key;

        public TValue Value { get; set; } = value;

        public Node? Left { get; set; }

        public Node? Right { get; set; }

        // A leaf has height 1 so an empty child can be 0
        public int Height { get; set; } = 1;
    }

    private readonly IComparer<TKey> _comparer;
    private Node? _root;

    public AvlTree(IComparer<TKey>? comparer = null) => _comparer = comparer ?? Comparer<TKey>.Default;

    public int Count { get; private set; }

    public int Height => HeightOf(_root);

    // How many rotations it has taken to stay balanced, handy for showing the tree working
    public long Rotations { get; private set; }

    // Returns false if the key was already there, in which case the value is replaced
    public bool Insert(TKey key, TValue value)
    {
        bool added = false;
        _root = Insert(_root, key, value, ref added);

        if (added)
        {
            Count++;
        }

        return added;
    }

    public bool Remove(TKey key)
    {
        bool removed = false;
        _root = Remove(_root, key, ref removed);

        if (removed)
        {
            Count--;
        }

        return removed;
    }

    // Used to trim the oldest reading once a node's history is full
    public bool RemoveMin()
    {
        if (_root is null)
        {
            return false;
        }

        return Remove(MinNode(_root).Key);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        Node? current = _root;

        while (current is not null)
        {
            int compare = _comparer.Compare(key, current.Key);

            if (compare == 0)
            {
                value = current.Value;
                return true;
            }

            current = compare < 0 ? current.Left : current.Right;
        }

        value = default!;
        return false;
    }

    public KeyValuePair<TKey, TValue>? Min() => _root is null ? null : Pair(MinNode(_root));

    public KeyValuePair<TKey, TValue>? Max()
    {
        if (_root is null)
        {
            return null;
        }

        Node current = _root;

        while (current.Right is not null)
        {
            current = current.Right;
        }

        return Pair(current);
    }

    public List<KeyValuePair<TKey, TValue>> Range(TKey from, TKey to)
    {
        List<KeyValuePair<TKey, TValue>> results = [];
        CollectRange(from, to, results);
        return results;
    }

    // In order walk that skips whole subtrees outside the window, returns how many nodes it had to look at
    public int CollectRange(TKey from, TKey to, ICollection<KeyValuePair<TKey, TValue>> into)
    {
        if (_comparer.Compare(from, to) > 0)
        {
            return 0;
        }

        return CollectRange(_root, from, to, into);
    }

    public IEnumerable<KeyValuePair<TKey, TValue>> InOrder()
    {
        // Explicit stack instead of recursion so it can yield one at a time
        Stack<Node> stack = new();
        Node? current = _root;

        while (current is not null || stack.Count > 0)
        {
            while (current is not null)
            {
                stack.Push(current);
                current = current.Left;
            }

            current = stack.Pop();
            yield return Pair(current);
            current = current.Right;
        }
    }

    // Walks the whole tree checking ordering, stored heights and balance, for the unit tests
    public bool IsValid() => Check(_root, out _) && InOrder().Count() == Count;

    private Node Insert(Node? node, TKey key, TValue value, ref bool added)
    {
        if (node is null)
        {
            added = true;
            return new Node(key, value);
        }

        int compare = _comparer.Compare(key, node.Key);

        if (compare < 0)
        {
            node.Left = Insert(node.Left, key, value, ref added);
        }
        else if (compare > 0)
        {
            node.Right = Insert(node.Right, key, value, ref added);
        }
        else
        {
            node.Value = value;
            return node;
        }

        // Rebalance on the way back up the recursion
        return Rebalance(node);
    }

    private Node? Remove(Node? node, TKey key, ref bool removed)
    {
        if (node is null)
        {
            return null;
        }

        int compare = _comparer.Compare(key, node.Key);

        if (compare < 0)
        {
            node.Left = Remove(node.Left, key, ref removed);
        }
        else if (compare > 0)
        {
            node.Right = Remove(node.Right, key, ref removed);
        }
        else
        {
            removed = true;

            // Zero or one child, the child just takes its place
            if (node.Left is null || node.Right is null)
            {
                return node.Left ?? node.Right;
            }

            // Two children, swap in the next key up (smallest on the right) and delete that one instead
            Node successor = MinNode(node.Right);
            Node replacement = new(successor.Key, successor.Value) { Left = node.Left };
            bool ignored = false;
            replacement.Right = Remove(node.Right, successor.Key, ref ignored);
            node = replacement;
        }

        return Rebalance(node);
    }

    private Node Rebalance(Node node)
    {
        UpdateHeight(node);
        int balance = BalanceOf(node);

        // Left heavy
        if (balance > 1)
        {
            // Left-right case, straighten it out into a left-left first
            if (BalanceOf(node.Left) < 0)
            {
                node.Left = RotateLeft(node.Left!);
            }

            return RotateRight(node);
        }

        // Right heavy
        if (balance < -1)
        {
            // Right-left case, straighten it out into a right-right first
            if (BalanceOf(node.Right) > 0)
            {
                node.Right = RotateRight(node.Right!);
            }

            return RotateLeft(node);
        }

        return node;
    }

    // The left child comes up and the old root becomes its right child
    private Node RotateRight(Node node)
    {
        Node pivot = node.Left!;
        node.Left = pivot.Right;
        pivot.Right = node;

        UpdateHeight(node);
        UpdateHeight(pivot);
        Rotations++;

        return pivot;
    }

    // Mirror of RotateRight
    private Node RotateLeft(Node node)
    {
        Node pivot = node.Right!;
        node.Right = pivot.Left;
        pivot.Left = node;

        UpdateHeight(node);
        UpdateHeight(pivot);
        Rotations++;

        return pivot;
    }

    private int CollectRange(Node? node, TKey from, TKey to, ICollection<KeyValuePair<TKey, TValue>> into)
    {
        if (node is null)
        {
            return 0;
        }

        int visited = 1;
        bool aboveFrom = _comparer.Compare(node.Key, from) > 0;
        bool belowTo = _comparer.Compare(node.Key, to) < 0;

        // Only go left if smaller keys could still be in the window, same idea on the right
        if (aboveFrom)
        {
            visited += CollectRange(node.Left, from, to, into);
        }

        if (_comparer.Compare(node.Key, from) >= 0 && _comparer.Compare(node.Key, to) <= 0)
        {
            into.Add(Pair(node));
        }

        if (belowTo)
        {
            visited += CollectRange(node.Right, from, to, into);
        }

        return visited;
    }

    private bool Check(Node? node, out int height)
    {
        height = 0;

        if (node is null)
        {
            return true;
        }

        if (!Check(node.Left, out int left) || !Check(node.Right, out int right))
        {
            return false;
        }

        height = Math.Max(left, right) + 1;

        bool ordered = (node.Left is null || _comparer.Compare(node.Left.Key, node.Key) < 0) &&
            (node.Right is null || _comparer.Compare(node.Right.Key, node.Key) > 0);

        return ordered && node.Height == height && Math.Abs(left - right) <= 1;
    }

    private static Node MinNode(Node node)
    {
        while (node.Left is not null)
        {
            node = node.Left;
        }

        return node;
    }

    private static int HeightOf(Node? node) => node?.Height ?? 0;

    // Positive means the left side is taller
    private static int BalanceOf(Node? node) => node is null ? 0 : HeightOf(node.Left) - HeightOf(node.Right);

    private static void UpdateHeight(Node node) => node.Height = Math.Max(HeightOf(node.Left), HeightOf(node.Right)) + 1;

    private static KeyValuePair<TKey, TValue> Pair(Node node) => new(node.Key, node.Value);
}
