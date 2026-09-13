namespace SmartX.Core;

// One store per value type. Mixing them would mean a List<object> and a box per reading.
public sealed class TelemetryStore
{
    public NodeTelemetryStore<float> Floats { get; } = new();

    public NodeTelemetryStore<int> Ints { get; } = new();

    public NodeTelemetryStore<bool> Bools { get; } = new();
}
