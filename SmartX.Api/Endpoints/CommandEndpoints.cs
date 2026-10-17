using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

// Manual overrides, the operator issues them here and devices collect them from /pending when they check in
public static class CommandEndpoints
{
    public static RouteGroupBuilder MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/commands").WithTags("Commands");

        group.MapPost("/", Issue);
        group.MapPost("/undo", Undo);
        group.MapGet("/history", GetHistory);
        group.MapGet("/pending/{nodeId}", CollectPending);

        return group;
    }

    private static async Task<IResult> Issue(
        IssueCommandDto dto,
        DeviceTable devices,
        CommandHistory history,
        CommandOutbox outbox,
        TimelineLog timeline,
        IHubContext<TelemetryHub> hub)
    {
        if (dto.Type == CommandType.Unspecified)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(dto.Type)] = ["A command type is required."]
            });
        }

        List<DeviceDto> targets = [];

        if (CommandTypes.IsFleetWide(dto.Type))
        {
            targets.AddRange(devices.Actuators(ActuatorKind.Valve));

            if (targets.Count == 0)
            {
                return Results.Problem("There are no valves registered to shut.", statusCode: 409);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.NodeId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(dto.NodeId)] = ["Pick a device to send the command to."]
                });
            }

            DeviceDto? device = devices.Get(dto.NodeId.Trim());

            if (device is null)
            {
                return Results.NotFound();
            }

            if (device.Actuator is not ActuatorKind kind)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(dto.NodeId)] = [$"{device.NodeId} is a sensor, it can't be switched."]
                });
            }

            if (!CommandTypes.Accepts(kind, dto.Type))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(dto.Type)] = [$"{device.NodeId} is a {kind.ToString().ToLowerInvariant()}, it can't take {dto.Type}."]
                });
            }

            targets.Add(device);
        }

        // Remember what each device was on before so undo can put it back, commanded wins since it is the newer intent
        DeviceCommand command = new()
        {
            Type = dto.Type,
            Targets = [.. targets.Select(d => new CommandTarget(d.NodeId, d.CommandedState ?? d.IsOn))]
        };

        foreach (CommandTarget target in command.Targets)
        {
            Send(outbox, devices, command.Id, target.NodeId, command.DesiredState);
        }

        history.Push(command);

        CommandRecordDto record = CommandRecordDto.From(command);

        await LogAsync(timeline, hub, TimelineEventKind.Command, command, $"{command.Type} sent to {Describe(command)}");
        await hub.Clients.All.SendAsync("CommandIssued", record);

        // Accepted rather than Ok because the device only applies it next time it checks in
        return Results.Accepted(value: record);
    }

    private static async Task<IResult> Undo(
        SensorRegistry registry,
        DeviceTable devices,
        CommandHistory history,
        CommandOutbox outbox,
        TimelineLog timeline,
        IHubContext<TelemetryHub> hub)
    {
        if (!history.TryUndo(out DeviceCommand? command))
        {
            return Results.Problem("There is nothing to undo.", statusCode: 409);
        }

        int skipped = 0;

        foreach (CommandTarget target in command!.Targets)
        {
            // Can't put back a state we never knew, or a device that has since been removed
            if (target.PreviousState is not bool previous || !registry.TryGet(target.NodeId, out _))
            {
                skipped++;
                continue;
            }

            Send(outbox, devices, command.Id, target.NodeId, previous);
        }

        string message = $"Undid {command.Type} on {Describe(command)}";

        if (skipped > 0)
        {
            message += $", {skipped} left as is";
        }

        CommandRecordDto record = CommandRecordDto.From(command, reverted: true);

        await LogAsync(timeline, hub, TimelineEventKind.Undo, command, message);
        await hub.Clients.All.SendAsync("CommandReverted", record);

        return Results.Ok(record);
    }

    private static IResult GetHistory(CommandHistory history, int limit = 50)
    {
        history.TryPeek(out DeviceCommand? next);

        return Results.Ok(new CommandHistoryDto
        {
            UndoDepth = history.UndoDepth,
            NextUndo = next is null ? null : CommandRecordDto.From(next),
            Commands = history.Recent(Math.Clamp(limit, 1, 100))
        });
    }

    // What a device calls when it checks in, everything waiting for it comes back oldest first
    private static IResult CollectPending(string nodeId, SensorRegistry registry, CommandOutbox outbox)
    {
        if (!registry.TryGet(nodeId, out _))
        {
            return Results.NotFound();
        }

        return Results.Ok(outbox.Collect(nodeId));
    }

    private static void Send(CommandOutbox outbox, DeviceTable devices, Guid commandId, string nodeId, bool state)
    {
        outbox.Enqueue(new PendingCommandDto
        {
            CommandId = commandId,
            NodeId = nodeId,
            DesiredState = state
        });

        devices.SetCommandedState(nodeId, state);
    }

    private static async Task LogAsync(
        TimelineLog timeline,
        IHubContext<TelemetryHub> hub,
        TimelineEventKind kind,
        DeviceCommand command,
        string message)
    {
        TimelineEventDto timelineEvent = new()
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Kind = kind,
            NodeId = command.Targets.Count == 1 ? command.Targets[0].NodeId : null,
            Message = message
        };

        timeline.LogEvent(timelineEvent);
        await hub.Clients.All.SendAsync("EventLogged", timelineEvent);
    }

    private static string Describe(DeviceCommand command) =>
        command.Targets.Count == 1 ? command.Targets[0].NodeId : $"{command.Targets.Count} devices";
}
