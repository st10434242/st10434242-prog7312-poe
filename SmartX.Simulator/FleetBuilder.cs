using SmartX.Core;

namespace SmartX.Simulator;

// Spreads sensors across a facility so the gateway's deployment tree has something to recurse over
public static class FleetBuilder
{
    public static List<SimulatedSensor> Build(SimulatorOptions options)
    {
        List<SimulatedSensor> fleet = [];
        int index = 0;

        for (int zone = 1; zone <= options.Zones; zone++)
        {
            for (int sub = 1; sub <= options.SubZonesPerZone; sub++)
            {
                string location = $"{options.Facility}/Zone{zone}/SubZone{sub}";

                for (int n = 1; n <= options.SensorsPerSubZone; n++)
                {
                    // Rotate the categories so every sub-zone gets a mix of all three
                    SensorCategory category = (SensorCategory)((index % 3) + 1);

                    string prefix = category switch
                    {
                        SensorCategory.Environmental => "SOIL",
                        SensorCategory.PowerConsumption => "METER",
                        _ => "VALVE"
                    };

                    // Soil moisture changes slowly, so it reports less often than the others
                    int everyNTicks = category == SensorCategory.Environmental ? 3 : 1;

                    fleet.Add(Create(prefix, zone, sub, n, location, category, everyNTicks, ref index));
                }

                // Every sub-zone gets its own irrigation pump and a relay feeding its meters
                fleet.Add(Create("PUMP", zone, sub, 1, location, SensorCategory.Actuator, 1, ref index));
                fleet.Add(Create("RELAY", zone, sub, 1, location, SensorCategory.Actuator, 1, ref index));
            }
        }

        return fleet;
    }

    private static SimulatedSensor Create(
        string prefix,
        int zone,
        int sub,
        int n,
        string location,
        SensorCategory category,
        int everyNTicks,
        ref int index)
    {
        index++;

        // Split over two bytes so a big load test fleet still gets valid MACs
        return new SimulatedSensor(
            nodeId: $"{prefix}-Z{zone}S{sub}-{n:00}",
            mac: $"AA:BB:{zone:X2}:{sub:X2}:{index >> 8:X2}:{index & 0xFF:X2}",
            location: location,
            category: category,
            publishEveryNTicks: everyNTicks);
    }
}
