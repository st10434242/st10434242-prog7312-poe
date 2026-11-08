namespace SmartX.Core;

// Binary min heap stored in a plain array, the children of index i sit at 2i + 1 and 2i + 2
public sealed class MinHeap<T>
{
    private const int DefaultCapacity = 16;

    private readonly IComparer<T> _comparer;
    private T[] _items;

    public MinHeap(IComparer<T>? comparer = null, int capacity = DefaultCapacity)
    {
        _comparer = comparer ?? Comparer<T>.Default;
        _items = new T[Math.Max(capacity, 1)];
    }

    // Heapify from the last parent down to the root, which is O(n) instead of n pushes at O(n log n)
    public MinHeap(IEnumerable<T> items, IComparer<T>? comparer = null)
    {
        _comparer = comparer ?? Comparer<T>.Default;
        _items = [.. items];
        Count = _items.Length;

        if (_items.Length == 0)
        {
            _items = new T[DefaultCapacity];
        }

        for (int i = (Count / 2) - 1; i >= 0; i--)
        {
            SiftDown(i);
        }
    }

    public int Count { get; private set; }

    public bool IsEmpty => Count == 0;

    public void Push(T item)
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
        }

        _items[Count] = item;
        SiftUp(Count);
        Count++;
    }

    public T Peek() => IsEmpty ? throw new InvalidOperationException("The heap is empty.") : _items[0];

    public T Pop()
    {
        if (!TryPop(out T? item))
        {
            throw new InvalidOperationException("The heap is empty.");
        }

        return item;
    }

    // Moves the last item to the root and lets it sink back down to where it belongs
    public bool TryPop(out T item)
    {
        if (IsEmpty)
        {
            item = default!;
            return false;
        }

        item = _items[0];
        Count--;
        _items[0] = _items[Count];
        _items[Count] = default!;

        if (Count > 0)
        {
            SiftDown(0);
        }

        return true;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;

            if (_comparer.Compare(_items[index], _items[parent]) >= 0)
            {
                return;
            }

            (_items[index], _items[parent]) = (_items[parent], _items[index]);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            int left = (2 * index) + 1;
            int right = left + 1;
            int smallest = index;

            if (left < Count && _comparer.Compare(_items[left], _items[smallest]) < 0)
            {
                smallest = left;
            }

            if (right < Count && _comparer.Compare(_items[right], _items[smallest]) < 0)
            {
                smallest = right;
            }

            if (smallest == index)
            {
                return;
            }

            (_items[index], _items[smallest]) = (_items[smallest], _items[index]);
            index = smallest;
        }
    }
}
