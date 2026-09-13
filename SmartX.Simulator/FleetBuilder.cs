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
                for (int n = 1; n <= options.SensorsPerSubZone; n++)
                {
                    // Rotate the categories so every sub-zone gets a mix of all three
                    SensorCategory category = (SensorCategory)((index % 3) + 1);
                    index++;

                    string prefix = category switch
                    {
                        SensorCategory.Environmental => "SOIL",
                        SensorCategory.PowerConsumption => "METER",
                        _ => "VALVE"
                    };

                    // Soil moisture changes slowly, so it reports less often than the others
                    int everyNTicks = category == SensorCategory.Environmental ? 3 : 1;

                    fleet.Add(new SimulatedSensor(
                        nodeId: $"{prefix}-Z{zone}S{sub}-{n:00}",
                        mac: $"AA:BB:CC:{zone:X2}:{sub:X2}:{index:X2}",
                        location: $"{options.Facility}/Zone{zone}/SubZone{sub}",
                        category: category,
                        publishEveryNTicks: everyNTicks));
                }
            }
        }

        return fleet;
    }
}
