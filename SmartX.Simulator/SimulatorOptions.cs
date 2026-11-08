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

    // Chance per reading that a soil sensor suddenly drops, like a blocked dripper
    public double DropChance { get; set; } = 0.02;

    // How fast soil moves towards wet or dry each reading, bigger means overrides show up quicker
    public double DryingRate { get; set; } = 0.04;

    // Chance per tick that a live sensor goes quiet, and that a quiet one comes back
    public double SilenceChance { get; set; } = 0.005;

    public double RecoverChance { get; set; } = 0.1;

    // Neighbour tables change slowly, so they go out less often than readings
    public int NeighbourReportEveryNTicks { get; set; } = 5;

    public int MaxNeighbours { get; set; } = 8;
}
