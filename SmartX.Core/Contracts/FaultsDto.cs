namespace SmartX.Core.Contracts;

public sealed record ActiveErrorDto(string NodeId, AlertKind Kind, AlertSeverity Severity);

public sealed record FaultsDto
{
    public required IReadOnlyList<string> Disconnected { get; init; }

    public required IReadOnlyList<ActiveErrorDto> ActiveErrors { get; init; }

    // How many repeat alerts the sets stopped, nice to show it is actually doing something
    public required long SuppressedDuplicates { get; init; }
}
