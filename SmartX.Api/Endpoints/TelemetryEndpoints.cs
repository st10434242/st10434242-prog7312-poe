using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

// Grouped in an extension method so Program.cs stays readable as more endpoints are added
public static class TelemetryEndpoints
{
    public static RouteGroupBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/telemetry").WithTags("Telemetry");

        group.MapPost("/", Ingest);
        group.MapPost("/batch", IngestBatch);
        group.MapGet("/nodes", GetNodes);
        group.MapGet("/{nodeId}", GetHistory);

        return group;
    }

    private static async Task<IResult> Ingest(
        TelemetryPacketDto dto,
        TelemetryStore store,
        SensorRegistry registry,
        NodeStateTracker tracker,
        FaultInjector faults,
        IHubContext<TelemetryHub> hub)
    {
        Dictionary<string, string[]> errors = Validate(dto, registry);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Silenced nodes are accepted and dropped, so the node believes it is still publishing
        if (faults.IsSilenced(dto.NodeId))
        {
            return Results.Accepted();
        }

        ReadingEventDto? reading = StoreReading(dto, store);

        if (reading is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(dto.ValueKind)] = [$"Unsupported value kind '{dto.ValueKind}'."]
            });
        }

        await BroadcastAsync(reading, tracker, hub);

        return Results.Accepted();
    }

    // One bad packet should not cost the whole batch, so each is judged on its own
    private static async Task<IResult> IngestBatch(
        TelemetryPacketDto[] packets,
        TelemetryStore store,
        SensorRegistry registry,
        NodeStateTracker tracker,
        FaultInjector faults,
        IHubContext<TelemetryHub> hub)
    {
        int accepted = 0;

        foreach (TelemetryPacketDto dto in packets)
        {
            if (Validate(dto, registry).Count > 0)
            {
                continue;
            }

            if (faults.IsSilenced(dto.NodeId))
            {
                accepted++;
                continue;
            }

            ReadingEventDto? reading = StoreReading(dto, store);

            if (reading is not null)
            {
                await BroadcastAsync(reading, tracker, hub);
                accepted++;
            }
        }

        return Results.Accepted(value: new BatchResultDto
        {
            Accepted = accepted,
            Rejected = packets.Length - accepted
        });
    }

    private static async Task BroadcastAsync(
        ReadingEventDto reading,
        NodeStateTracker tracker,
        IHubContext<TelemetryHub> hub)
    {
        // A packet just arrived, so by definition the node is live
        tracker.Update(reading.NodeId, LivenessState.Live);

        await hub.Clients.All.SendAsync("ReadingReceived", reading);
    }

    private static IResult GetNodes(TelemetryStore store)
    {
        List<string> nodes =
        [
            .. store.Floats.NodeIds(),
            .. store.Ints.NodeIds(),
            .. store.Bools.NodeIds()
        ];

        return Results.Ok(nodes.Order());
    }

    private static IResult GetHistory(string nodeId, TelemetryStore store, SensorRegistry registry)
    {
        if (!registry.TryGet(nodeId, out SensorRegistration? registration))
        {
            return Results.NotFound();
        }

        // The registration says which typed store holds this node
        IEnumerable<ReadingDto> readings = registration.Category switch
        {
            SensorCategory.Environmental => store.Floats.GetRecent(nodeId).Select(ToReading),
            SensorCategory.PowerConsumption => store.Ints.GetRecent(nodeId).Select(ToReading),
            SensorCategory.Actuator => store.Bools.GetRecent(nodeId).Select(ToReading),
            _ => []
        };

        List<ReadingDto> ordered = [.. readings];

        return Results.Ok(new NodeHistoryDto
        {
            NodeId = nodeId,
            Count = ordered.Count,
            Readings = ordered
        });
    }

    // The only place an untyped payload becomes a typed packet
    private static ReadingEventDto? StoreReading(TelemetryPacketDto dto, TelemetryStore store)
    {
        switch (dto.ValueKind)
        {
            case TelemetryValueKind.Float:
                dto.TryReadFloat(out float floatValue);
                store.Floats.Add(ToPacket(dto, floatValue));
                return ToEvent(dto, floatValue, floatValue);

            case TelemetryValueKind.Int:
                dto.TryReadInt(out int intValue);
                store.Ints.Add(ToPacket(dto, intValue));
                return ToEvent(dto, intValue, intValue);

            case TelemetryValueKind.Bool:
                dto.TryReadBool(out bool boolValue);
                store.Bools.Add(ToPacket(dto, boolValue));
                return ToEvent(dto, boolValue, 0);

            default:
                return null;
        }
    }

    private static ReadingEventDto ToEvent(TelemetryPacketDto dto, object value, double numeric) => new()
    {
        NodeId = dto.NodeId,
        TimestampUtc = dto.TimestampUtc,
        Value = value,
        Band = NodeStatus.BandFor(dto.Category, numeric)
    };

    private static TelemetryPacket<T> ToPacket<T>(TelemetryPacketDto dto, T value) where T : struct =>
        new(dto.NodeId, dto.TimestampUtc, dto.Category, value);

    private static ReadingDto ToReading<T>(TelemetryPacket<T> packet) where T : struct =>
        new() { TimestampUtc = packet.TimestampUtc, Value = packet.Value };

    // Minimal APIs do not validate automatically
    private static Dictionary<string, string[]> Validate(TelemetryPacketDto dto, SensorRegistry registry)
    {
        Dictionary<string, string[]> errors = [];

        if (string.IsNullOrWhiteSpace(dto.NodeId))
        {
            errors[nameof(dto.NodeId)] = ["A node id is required."];
            return errors;
        }

        // The registration is the authority now, not what the packet claims about itself
        if (!registry.TryGet(dto.NodeId, out SensorRegistration? registration))
        {
            errors[nameof(dto.NodeId)] = [$"Node '{dto.NodeId}' is not registered."];
            return errors;
        }

        if (dto.Category != registration.Category)
        {
            errors[nameof(dto.Category)] =
                [$"Node '{dto.NodeId}' is registered as {registration.Category}, not {dto.Category}."];
        }

        TelemetryValueKind expected = TelemetryPacketDto.ExpectedKindFor(registration.Category);

        if (dto.ValueKind != expected)
        {
            errors[nameof(dto.ValueKind)] =
                [$"A {registration.Category} sensor publishes {expected}, not {dto.ValueKind}."];
        }
        else if (!dto.HasConsistentValue())
        {
            errors[nameof(dto.Value)] = [$"The value cannot be read as {dto.ValueKind}."];
        }

        return errors;
    }
}
