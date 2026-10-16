namespace SmartX.Core;

// One device a command touched and what it was before, so undo knows what to put back
public sealed record CommandTarget(string NodeId, bool? PreviousState);

// A manual override sent by an operator, one command can hit several devices
public sealed record DeviceCommand
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required CommandType Type { get; init; }

    public required IReadOnlyList<CommandTarget> Targets { get; init; }

    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool DesiredState => CommandTypes.DesiredState(Type);
}
