namespace SmartX.Simulator;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public string ApiBaseUrl { get; set; } = "http://localhost:5225";

    public int PublishIntervalMs { get; set; } = 1000;

    public string NodeId { get; set; } = "ESP32-7F2A";
}
