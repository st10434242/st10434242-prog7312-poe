namespace SmartX.Simulator;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public string ApiBaseUrl { get; set; } = "http://localhost:5225";

    public int PublishIntervalMs { get; set; } = 1000;

    public string Facility { get; set; } = "FacilityA";

    public int Zones { get; set; } = 3;

    public int SubZonesPerZone { get; set; } = 2;

    public int SensorsPerSubZone { get; set; } = 4;

    // Chance per reading that a sensor reports a sudden spike
    public double SpikeChance { get; set; } = 0.02;

    // Chance per tick that a live sensor goes quiet, and that a quiet one comes back
    public double SilenceChance { get; set; } = 0.005;

    public double RecoverChance { get; set; } = 0.1;
}
