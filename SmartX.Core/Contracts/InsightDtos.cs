using System.Text.Json.Serialization;

namespace SmartX.Core.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<InsightKind>))]
public enum InsightKind
{
    // Comes with a command the operator can apply in one click
    SuggestedAction = 1,

    // Just points at a device worth looking at
    Highlight = 2
}

[JsonConverter(typeof(JsonStringEnumConverter<InsightSource>))]
public enum InsightSource
{
    Rule = 1,
    Learned = 2,
    Combined = 3
}

[JsonConverter(typeof(JsonStringEnumConverter<ActivityKind>))]
public enum ActivityKind
{
    Search = 1,
    View = 2,
    Command = 3,
    Alert = 4
}

public sealed record InsightDto
{
    // Built from the action and device so the same suggestion keeps the same id between refreshes
    public required string Id { get; init; }

    public required InsightKind Kind { get; init; }

    public required InsightSource Source { get; init; }

    public required string Title { get; init; }

    public required string Reason { get; init; }

    public required int Score { get; init; }

    public AlertSeverity? Severity { get; init; }

    public required string NodeId { get; init; }

    public string? TriggerNodeId { get; init; }

    public IssueCommandDto? Command { get; init; }
}

// What the dashboard posts when the operator searches or opens a device
public sealed record ActivityDto
{
    public required ActivityKind Kind { get; init; }

    public required string Term { get; init; }
}

public sealed record PatternDto(string From, string To, int Count, double Confidence);
