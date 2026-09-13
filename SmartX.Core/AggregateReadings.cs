using System.Numerics;

namespace SmartX.Core;

// Aggregation is constrained to numbers because adding two valve states is meaningless, which is
// also why these operators cannot live on TelemetryPacket<T>, since that has to accept bool too.
public readonly struct MeterReading<T> where T : struct, INumber<T>
{
    public T Value { get; }

    public MeterReading(T value) => Value = value;

    public static MeterReading<T> operator +(MeterReading<T> left, MeterReading<T> right) =>
        new(left.Value + right.Value);

    public static MeterReading<T> operator -(MeterReading<T> left, MeterReading<T> right) =>
        new(left.Value - right.Value);

    public static bool operator >(MeterReading<T> left, MeterReading<T> right) =>
        left.Value > right.Value;

    public static bool operator <(MeterReading<T> left, MeterReading<T> right) =>
        left.Value < right.Value;

    public override string ToString() => Value.ToString() ?? string.Empty;
}

// Actuators aggregate with logic rather than arithmetic: are all valves open, is any valve open
public readonly struct ActuatorReading
{
    public bool IsOpen { get; }

    public ActuatorReading(bool isOpen) => IsOpen = isOpen;

    public static ActuatorReading operator &(ActuatorReading left, ActuatorReading right) =>
        new(left.IsOpen && right.IsOpen);

    public static ActuatorReading operator |(ActuatorReading left, ActuatorReading right) =>
        new(left.IsOpen || right.IsOpen);

    public override string ToString() => IsOpen ? "open" : "closed";
}
