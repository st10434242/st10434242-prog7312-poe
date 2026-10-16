namespace SmartX.Core.Contracts;

// Pushed to the dashboard when a reading crosses a critical line or a node drops off
public sealed record AlertDto
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string NodeId { get; init; }

    public required AlertKind Kind { get; init; }

    public required AlertSeverity Severity { get; init; }

    // Null for disconnects since there is no reading to show
    public double? Value { get; init; }

    public required string Message { get; init; }

    public required DateTimeOffset RaisedAtUtc { get; init; }
}
