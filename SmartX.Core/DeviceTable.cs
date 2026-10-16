using SmartX.Core.Contracts;

namespace SmartX.Core;

// Every live device keyed by node id, so finding the right one when a packet lands is a single O(1) lookup
public sealed class DeviceTable
{
    private readonly Dictionary<string, DeviceState> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _devices.Count;
            }
        }
    }

    public void Add(SensorRegistration registration)
    {
        lock (_gate)
        {
            // TryAdd so a re-register does not wipe the state we already have
            _devices.TryAdd(registration.NodeId, new DeviceState
            {
                NodeId = registration.NodeId,
                Location = registration.Location.ToString(),
                Category = registration.Category
            });
        }
    }

    public bool Remove(string nodeId)
    {
        lock (_gate)
        {
            return _devices.Remove(nodeId);
        }
    }

    public DeviceDto? Get(string nodeId)
    {
        lock (_gate)
        {
            return _devices.TryGetValue(nodeId, out DeviceState? state) ? DeviceDto.From(state) : null;
        }
    }

    // Called for every packet the processor handles
    public void RecordReading(ReadingEventDto reading, bool alerted)
    {
        lock (_gate)
        {
            if (!_devices.TryGetValue(reading.NodeId, out DeviceState? state))
            {
                return;
            }

            if (reading.Value is bool on)
            {
                state.IsOn = on;
            }
            else
            {
                // Floats come through with noise like 35.72309112548828 so trim it
                state.LastValue = Math.Round(Convert.ToDouble(reading.Value), 2);
            }

            state.Band = reading.Band;
            state.Liveness = LivenessState.Live;
            state.LastSeenUtc = reading.TimestampUtc;
            state.PacketCount++;

            if (alerted)
            {
                state.ActiveAlerts++;
            }
            else if (reading.Band == ThresholdBand.Normal)
            {
                state.ActiveAlerts = 0;
            }
        }
    }

    public void SetLiveness(string nodeId, LivenessState liveness)
    {
        lock (_gate)
        {
            if (_devices.TryGetValue(nodeId, out DeviceState? state))
            {
                state.Liveness = liveness;
            }
        }
    }

    // All filters are optional, anything left null matches everything
    public IReadOnlyList<DeviceDto> Find(SensorCategory? category, DeviceAlertState? alertState, string? search)
    {
        lock (_gate)
        {
            List<DeviceDto> matches = [];

            foreach (DeviceState state in _devices.Values)
            {
                if (category is not null && state.Category != category)
                {
                    continue;
                }

                if (alertState is not null && state.AlertState != alertState)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(search) &&
                    !state.NodeId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                    !state.Location.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matches.Add(DeviceDto.From(state));
            }

            // Worst devices first so problems are at the top of the list
            return [.. matches.OrderByDescending(d => d.AlertState).ThenBy(d => d.NodeId)];
        }
    }
}
