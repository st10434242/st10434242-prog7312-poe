namespace SmartX.Core;

// Tree key for a reading, the sequence breaks ties so two readings stamped the same tick both get stored
public readonly record struct TelemetryKey(DateTimeOffset TimestampUtc, long Sequence) : IComparable<TelemetryKey>
{
    // Bounds for a range query, every key at a given time sits between these two
    public static TelemetryKey Earliest(DateTimeOffset at) => new(at, long.MinValue);

    public static TelemetryKey Latest(DateTimeOffset at) => new(at, long.MaxValue);

    public int CompareTo(TelemetryKey other)
    {
        int byTime = TimestampUtc.CompareTo(other.TimestampUtc);
        return byTime != 0 ? byTime : Sequence.CompareTo(other.Sequence);
    }
}
