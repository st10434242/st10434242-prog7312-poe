namespace SmartX.Core;

// One store per value type. Mixing them would mean a List<object> and a box per reading.
public sealed class TelemetryStore
{
    public NodeTelemetryStore<float> Floats { get; } = new();

    public NodeTelemetryStore<int> Ints { get; } = new();

    public NodeTelemetryStore<bool> Bools { get; } = new();

    // The registered category says which typed store holds this node
    public DateTimeOffset? LastSeenUtc(SensorRegistration sensor)
    {
        return sensor.Category switch
        {
            SensorCategory.Environmental => Last(Floats, sensor.NodeId),
            SensorCategory.PowerConsumption => Last(Ints, sensor.NodeId),
            SensorCategory.Actuator => Last(Bools, sensor.NodeId),
            _ => null
        };

        static DateTimeOffset? Last<T>(NodeTelemetryStore<T> store, string nodeId) where T : struct
        {
            IReadOnlyList<TelemetryPacket<T>> recent = store.GetRecent(nodeId);
            return recent.Count == 0 ? null : recent[^1].TimestampUtc;
        }
    }
}
