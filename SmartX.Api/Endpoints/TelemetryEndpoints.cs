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

    private static IResult Ingest(TelemetryPacketDto dto, TelemetryStore store, SensorRegistry registry)
    {
        Dictionary<string, string[]> errors = Validate(dto, registry);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (!TryStore(dto, store))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(dto.ValueKind)] = [$"Unsupported value kind '{dto.ValueKind}'."]
            });
        }

        return Results.Accepted();
    }

    // One bad packet should not cost the whole batch, so each is judged on its own
    private static IResult IngestBatch(
        TelemetryPacketDto[] packets,
        TelemetryStore store,
        SensorRegistry registry)
    {
        int accepted = 0;

        foreach (TelemetryPacketDto dto in packets)
        {
            if (Validate(dto, registry).Count == 0 && TryStore(dto, store))
            {
                accepted++;
            }
        }

        return Results.Accepted(value: new BatchResultDto
        {
            Accepted = accepted,
            Rejected = packets.Length - accepted
        });
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
    private static bool TryStore(TelemetryPacketDto dto, TelemetryStore store)
    {
        switch (dto.ValueKind)
        {
            case TelemetryValueKind.Float:
                dto.TryReadFloat(out float floatValue);
                store.Floats.Add(ToPacket(dto, floatValue));
                return true;

            case TelemetryValueKind.Int:
                dto.TryReadInt(out int intValue);
                store.Ints.Add(ToPacket(dto, intValue));
                return true;

            case TelemetryValueKind.Bool:
                dto.TryReadBool(out bool boolValue);
                store.Bools.Add(ToPacket(dto, boolValue));
                return true;

            default:
                return false;
        }
    }

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
