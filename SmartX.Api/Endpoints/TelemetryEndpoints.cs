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

    // Validated here but only queued, the QueueProcessor does the storing and broadcasting
    private static IResult Ingest(
        TelemetryPacketDto dto,
        SensorRegistry registry,
        FaultInjector faults,
        TelemetryQueue queue)
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

        if (!queue.TryEnqueue(dto))
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Accepted();
    }

    // One bad packet should not cost the whole batch, so each is judged on its own
    private static IResult IngestBatch(
        TelemetryPacketDto[] packets,
        SensorRegistry registry,
        FaultInjector faults,
        TelemetryQueue queue)
    {
        int accepted = 0;

        foreach (TelemetryPacketDto dto in packets)
        {
            if (Validate(dto, registry).Count > 0)
            {
                continue;
            }

            if (faults.IsSilenced(dto.NodeId) || queue.TryEnqueue(dto))
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
