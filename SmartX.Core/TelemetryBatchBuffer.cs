namespace SmartX.Core;

// Staging area so ingestion is a cheap array write. Jagged because a drain can land part way through a row, so the rows end up different lengths.

public sealed class TelemetryBatchBuffer<T> where T : struct
{
    private readonly TelemetryPacket<T>[][] _rows;
    private readonly int _rowSize;

    private int _row;
    private int _col;

    public TelemetryBatchBuffer(int rowSize = 32, int rows = 8)
    {
        _rowSize = rowSize;
        _rows = new TelemetryPacket<T>[rows][];
        _rows[0] = new TelemetryPacket<T>[rowSize];
    }

    public int Count => (_row * _rowSize) + _col;

    public bool IsFull => Count == _rows.Length * _rowSize;

    public void Add(TelemetryPacket<T> packet)
    {
        if (IsFull)
        {
            throw new InvalidOperationException("Batch buffer is full, drain it first.");
        }

        if (_col == _rowSize)
        {
            _row++;
            _rows[_row] ??= new TelemetryPacket<T>[_rowSize];
            _col = 0;
        }

        _rows[_row][_col] = packet;
        _col++;
    }

    // Flattens the staged rows into a List<T> and resets
    public List<TelemetryPacket<T>> Drain()
    {
        List<TelemetryPacket<T>> drained = new(Count);

        for (int row = 0; row <= _row; row++)
        {
            int length = row == _row ? _col : _rowSize;

            for (int i = 0; i < length; i++)
            {
                drained.Add(_rows[row][i]);
            }
        }

        _row = 0;
        _col = 0;

        return drained;
    }
}
