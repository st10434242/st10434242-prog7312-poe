namespace SmartX.Core.Contracts;

// What the dashboard posts, NodeId is left out for fleet wide commands like ShutAllValves
public sealed record IssueCommandDto
{
    public string? NodeId { get; init; }

    public required CommandType Type { get; init; }
}

public sealed record CommandRecordDto
{
    public required Guid Id { get; init; }

    public required CommandType Type { get; init; }

    public required IReadOnlyList<string> NodeIds { get; init; }

    public required bool DesiredState { get; init; }

    public required DateTimeOffset IssuedAtUtc { get; init; }

    public bool Reverted { get; init; }

    public static CommandRecordDto From(DeviceCommand command, bool reverted = false) => new()
    {
        Id = command.Id,
        Type = command.Type,
        NodeIds = [.. command.Targets.Select(t => t.NodeId)],
        DesiredState = command.DesiredState,
        IssuedAtUtc = command.IssuedAtUtc,
        Reverted = reverted
    };
}

public sealed record CommandHistoryDto
{
    public required int UndoDepth { get; init; }

    // The one that undo would revert, so the button can say what it will do
    public CommandRecordDto? NextUndo { get; init; }

    public required IReadOnlyList<CommandRecordDto> Commands { get; init; }
}

// What a device picks up when it checks in, it only cares about the state to switch to
public sealed record PendingCommandDto
{
    public required Guid CommandId { get; init; }

    public required string NodeId { get; init; }

    public required bool DesiredState { get; init; }
}
