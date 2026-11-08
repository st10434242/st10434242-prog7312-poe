namespace SmartX.Core;

// Makes up a believable past for a node so there is a full day of history to query from the moment it registers
public static class SyntheticHistory
{
    public static IEnumerable<HistoryReading> For(
        SensorRegistration sensor,
        DateTimeOffset fromUtc,
        DateTimeOffset untilUtc,
        TimeSpan step)
    {
        // Seeded from the node id so the same node always gets the same past
        Random random = new(StableSeed(sensor.NodeId));
        double moisture = 40 + (random.NextDouble() * 15);

        for (DateTimeOffset at = fromUtc; at < untilUtc; at += step)
        {
            double hour = at.ToLocalTime().TimeOfDay.TotalHours;

            double value = sensor.Category switch
            {
                SensorCategory.Environmental => NextMoisture(ref moisture, hour, step, random),
                SensorCategory.PowerConsumption => NextPower(hour, random),
                SensorCategory.Actuator => IrrigationWindow(hour) ? 1 : 0,
                _ => 0
            };

            yield return new HistoryReading(at, Math.Round(value, 2));
        }
    }

    // Watering runs for half an hour every six hours, matching how the valves and pumps behave
    private static bool IrrigationWindow(double hour) => hour % 6 < 0.5;

    // Dries out slowly between waterings and jumps back up while the water runs, with the odd blocked dripper
    private static double NextMoisture(ref double moisture, double hour, TimeSpan step, Random random)
    {
        double hours = step.TotalHours;

        moisture += IrrigationWindow(hour) ? 40 * hours : -2.5 * hours;
        moisture = Math.Clamp(moisture + ((random.NextDouble() - 0.5) * 0.4), 15, 60);

        double reading = moisture + ((random.NextDouble() - 0.5) * 1.5);

        if (random.NextDouble() < 0.002)
        {
            reading -= 25;
        }

        return Math.Clamp(reading, 0, 100);
    }

    // Grow lights run from 06:00 to 20:00, so the meters follow a day curve with the occasional spike
    private static double NextPower(double hour, Random random)
    {
        bool lightsOn = hour is >= 6 and < 20;
        double baseline = lightsOn ? 550 + (150 * Math.Sin((hour - 6) / 14 * Math.PI)) : 120;
        double reading = baseline + ((random.NextDouble() - 0.5) * 60);

        if (random.NextDouble() < 0.002)
        {
            reading += 500;
        }

        return Math.Max(0, Math.Round(reading));
    }

    // string.GetHashCode changes every run, this one doesn't
    private static int StableSeed(string text)
    {
        uint hash = 2166136261;

        foreach (char c in text)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return (int)hash;
    }
}
