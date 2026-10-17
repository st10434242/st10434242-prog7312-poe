using System.Text.Json;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Simulator;

// What the rest of the sub-zone is doing this tick, so overrides actually change what the sensors report
public readonly record struct SubZoneConditions(bool Irrigating, bool Powered);

// One fake device. It keeps its own baseline so readings drift like hardware instead of jumping
// around at random, which is what makes a spike actually look like a spike on a sparkline.
public sealed class SimulatedSensor
{
    // An ESP32 only has so much RAM to hold readings while the gateway can't hear it
    private const int BacklogLimit = 30;

    private readonly double _min;
    private readonly double _max;
    private readonly double _noise;
    private readonly double _spikeSize;
    private readonly Queue<TelemetryPacketDto> _backlog = new();

    private double _baseline;

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
        Actuator = category == SensorCategory.Actuator ? CommandTypes.KindFor(nodeId) : null;

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

    public ActuatorKind? Actuator { get; }

    public int PublishEveryNTicks { get; }

    public bool IsSilent { get; private set; }

    // Everything starts switched on, after that only a command changes it
    public bool IsOn { get; private set; } = true;

    public int BacklogCount => _backlog.Count;

    public bool IsDue(long tick) => tick % PublishEveryNTicks == 0;

    public void UpdateFaults(double silenceChance, double recoverChance)
    {
        IsSilent = IsSilent
            ? Random.Shared.NextDouble() >= recoverChance
            : Random.Shared.NextDouble() < silenceChance;
    }

    // Returns true if the command actually changed anything
    public bool Apply(PendingCommandDto command)
    {
        bool changed = IsOn != command.DesiredState;
        IsOn = command.DesiredState;
        return changed;
    }

    // Store and forward, a silent device keeps measuring and sends it all once it is back
    public void Hold(TelemetryPacketDto reading)
    {
        _backlog.Enqueue(reading);

        if (_backlog.Count > BacklogLimit)
        {
            _backlog.Dequeue();
        }
    }

    public List<TelemetryPacketDto> FlushBacklog()
    {
        List<TelemetryPacketDto> held = [.. _backlog];
        _backlog.Clear();
        return held;
    }

    public TelemetryPacketDto NextReading(SimulatorOptions options, SubZoneConditions conditions, out bool spiked)
    {
        spiked = false;

        switch (Category)
        {
            case SensorCategory.Actuator:
                return Packet(TelemetryValueKind.Bool, IsOn);

            case SensorCategory.Environmental:
                return Packet(TelemetryValueKind.Float, (float)NextMoisture(options, conditions.Irrigating, out spiked));

            default:
                return Packet(TelemetryValueKind.Int, (int)Math.Round(NextPower(options, conditions.Powered, out spiked)));
        }
    }

    private double NextMoisture(SimulatorOptions options, bool irrigating, out bool spiked)
    {
        spiked = false;

        // Soil drifts towards wet while water is flowing and slowly dries out when it isn't
        double target = irrigating ? (_min + _max) / 2 : 10;
        _baseline += (target - _baseline) * options.DryingRate;
        _baseline += (Random.Shared.NextDouble() - 0.5) * _noise;
        _baseline = Math.Clamp(_baseline, 5, 95);

        double value = _baseline + ((Random.Shared.NextDouble() - 0.5) * _noise);
        double roll = Random.Shared.NextDouble();

        if (roll < options.SpikeChance)
        {
            // Flooded tray or a burst line
            value += _spikeSize;
            spiked = true;
        }
        else if (roll < options.SpikeChance + options.DropChance)
        {
            // Blocked dripper, the reading falls off a cliff
            value -= 25 + (Random.Shared.NextDouble() * 10);
            spiked = true;
        }

        return Math.Clamp(value, 0, 100);
    }

    private double NextPower(SimulatorOptions options, bool powered, out bool spiked)
    {
        spiked = false;

        // Relay is off so the meter only sees standby draw
        if (!powered)
        {
            return 10 + (Random.Shared.NextDouble() * 15);
        }

        // Random walk, clamped so the baseline cannot wander off
        _baseline = Math.Clamp(_baseline + ((Random.Shared.NextDouble() - 0.5) * _noise), _min, _max);

        double value = _baseline + ((Random.Shared.NextDouble() - 0.5) * _noise);

        if (Random.Shared.NextDouble() < options.SpikeChance)
        {
            // Spike size varies so some only warn and some go critical
            value += _spikeSize * (0.5 + Random.Shared.NextDouble());
            spiked = true;
        }

        return value;
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
