namespace SmartX.Core;

// A sensor known to the gateway, telemetry is only accepted from registered nodes
public sealed record SensorRegistration
{
    public static readonly TimeSpan MinExpectedInterval = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan MaxExpectedInterval = TimeSpan.FromMinutes(15);

    public required string NodeId { get; init; }

    public required MacAddress Mac { get; init; }

    public required DeploymentPath Location { get; init; }

    public required SensorCategory Category { get; init; }

    // Staleness is judged against this rather than one global timeout, since every node publishes at its own rate
    public required TimeSpan ExpectedInterval { get; init; }

    public DateTimeOffset RegisteredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
