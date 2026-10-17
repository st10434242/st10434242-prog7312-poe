using SmartX.Core.Contracts;

namespace SmartX.Core;

// One thing that happened, either the operator doing something or the network raising an alert
public sealed record OperatorActivity
{
    public required ActivityKind Kind { get; init; }

    // What the learner counts on, e.g. alert:MoistureDrop:SOIL-Z1S1-01 or command:StartPump:PUMP-Z1S1-01
    public required string Key { get; init; }

    public string? NodeId { get; init; }

    public CommandType? Command { get; init; }

    public AlertKind? Alert { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool IsOperator => Kind != ActivityKind.Alert;

    // Searching and opening a device are the same intent so they share a key
    public static OperatorActivity Search(string term, string? nodeId, ActivityKind kind = ActivityKind.Search) => new()
    {
        Kind = kind,
        Key = $"search:{(nodeId ?? term).Trim().ToUpperInvariant()}",
        NodeId = nodeId
    };

    public static OperatorActivity ForCommand(CommandType type, string? nodeId) => new()
    {
        Kind = ActivityKind.Command,
        Key = $"command:{type}:{(nodeId ?? "ALL").ToUpperInvariant()}",
        NodeId = nodeId,
        Command = type
    };

    public static OperatorActivity ForAlert(AlertKind kind, string nodeId) => new()
    {
        Kind = ActivityKind.Alert,
        Key = $"alert:{kind}:{nodeId.ToUpperInvariant()}",
        NodeId = nodeId,
        Alert = kind
    };

    public string Describe() => Kind switch
    {
        ActivityKind.Alert => $"{Alert} on {NodeId}",
        ActivityKind.Command => NodeId is null ? $"{Command}" : $"{Command} on {NodeId}",
        _ => $"looking at {NodeId ?? Key[7..]}"
    };
}
