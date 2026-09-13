using System.Text.Json;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Simulator;

// One fake device. It keeps its own baseline so readings drift like hardware instead of jumping
// around at random, which is what makes a spike actually look like a spike on a sparkline.
public sealed class SimulatedSensor
{
    private readonly double _min;
    private readonly double _max;
    private readonly double _noise;
    private readonly double _spikeSize;

    private double _baseline;
    private bool _valveOpen = true;

    public SimulatedSensor(
        string nodeId,
        string mac,
        string location,
        SensorCategory category,
        int publishEveryNTicks)
    {
        NodeId = nodeId;
        Mac = mac;
        Location = location;
        Category = category;
        PublishEveryNTicks = publishEveryNTicks;

        (_min, _max, _noise, _spikeSize) = category switch
        {
            SensorCategory.Environmental => (35d, 55d, 1.5d, 45d),      // soil moisture percent
            SensorCategory.PowerConsumption => (400d, 750d, 30d, 600d), // watts
            _ => (0d, 1d, 0d, 0d)
        };

        _baseline = _min + (Random.Shared.NextDouble() * (_max - _min));
    }

    public string NodeId { get; }

    public string Mac { get; }

    public string Location { get; }

    public SensorCategory Category { get; }

    public int PublishEveryNTicks { get; }

    public bool IsSilent { get; private set; }

    public bool ShouldPublish(long tick) => !IsSilent && tick % PublishEveryNTicks == 0;

    public void UpdateFaults(double silenceChance, double recoverChance)
    {
        IsSilent = IsSilent
            ? Random.Shared.NextDouble() >= recoverChance
            : Random.Shared.NextDouble() < silenceChance;
    }

    public TelemetryPacketDto NextReading(double spikeChance, out bool spiked)
    {
        spiked = false;

        if (Category == SensorCategory.Actuator)
        {
            // Valves hold their position rather than flipping every tick
            if (Random.Shared.NextDouble() < 0.1)
            {
                _valveOpen = !_valveOpen;
            }

            return Packet(TelemetryValueKind.Bool, _valveOpen);
        }

        // Random walk, clamped so the baseline cannot wander off
        _baseline = Math.Clamp(_baseline + ((Random.Shared.NextDouble() - 0.5) * _noise), _min, _max);

        double value = _baseline + ((Random.Shared.NextDouble() - 0.5) * _noise);

        if (Random.Shared.NextDouble() < spikeChance)
        {
            value += _spikeSize;
            spiked = true;
        }

        return Category == SensorCategory.PowerConsumption
            ? Packet(TelemetryValueKind.Int, (int)Math.Round(value))
            : Packet(TelemetryValueKind.Float, (float)value);
    }

    private TelemetryPacketDto Packet(TelemetryValueKind kind, object value) => new()
    {
        NodeId = NodeId,
        TimestampUtc = DateTimeOffset.UtcNow,
        Category = Category,
        ValueKind = kind,
        Value = JsonSerializer.SerializeToElement(value)
    };
}
