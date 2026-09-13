using System.Globalization;

namespace SmartX.Core;

// Constraining T to a struct lets one type carry float, int or bool without boxing
public readonly struct TelemetryPacket<T> where T : struct
{
    public string NodeId { get; }

    public DateTimeOffset TimestampUtc { get; }

    public SensorCategory Category { get; }

    public T Value { get; }

    public TelemetryPacket(
        string nodeId,
        DateTimeOffset timestampUtc,
        SensorCategory category,
        T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        NodeId = nodeId;
        TimestampUtc = timestampUtc;
        Category = category;
        Value = value;
    }

    // Takes the clock as a parameter instead of reading UtcNow so staleness stays testable
    public TimeSpan AgeAt(DateTimeOffset now) => now - TimestampUtc;

    public override string ToString() =>
        $"{NodeId} [{Category}] {FormatValue()} @ {TimestampUtc:O}";

    // Invariant because en-ZA formats 42.5 as "42,5"
    private string FormatValue() =>
        Value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : Value.ToString() ?? string.Empty;
}
