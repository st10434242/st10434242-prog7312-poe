using System.Collections;

namespace SmartX.Core;

// Keeps the last N readings and overwrites the oldest, which is the window a sparkline draws
public sealed class TelemetryRingBuffer<TItem> : IReadOnlyCollection<TItem>
{
    private readonly TItem[] _items;
    private int _start;
    private int _count;

    public TelemetryRingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);

        _items = new TItem[capacity];
    }

    public int Count => _count;

    public void Add(TItem item)
    {
        if (_count < _items.Length)
        {
            _items[(_start + _count) % _items.Length] = item;
            _count++;
            return;
        }

        // Full, so overwrite the oldest and slide the window forward
        _items[_start] = item;
        _start = (_start + 1) % _items.Length;
    }

    // Oldest first
    public IEnumerator<TItem> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return _items[(_start + i) % _items.Length];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
