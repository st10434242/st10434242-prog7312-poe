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
        group.MapGet("/nodes", GetNodes);
        group.MapGet("/{nodeId}", GetHistory);

        return group;
    }

    private static IResult Ingest(TelemetryPacketDto dto, TelemetryStore store)
    {
        Dictionary<string, string[]> errors = Validate(dto);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // The only place an untyped payload becomes a typed packet
        switch (dto.ValueKind)
        {
            case TelemetryValueKind.Float:
                dto.TryReadFloat(out float floatValue);
                store.Floats.Add(ToPacket(dto, floatValue));
                break;

            case TelemetryValueKind.Int:
                dto.TryReadInt(out int intValue);
                store.Ints.Add(ToPacket(dto, intValue));
                break;

            case TelemetryValueKind.Bool:
                dto.TryReadBool(out bool boolValue);
                store.Bools.Add(ToPacket(dto, boolValue));
                break;

            // Without this a kind with no case would fall through and still answer 202
            default:
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(dto.ValueKind)] = [$"Unsupported value kind '{dto.ValueKind}'."]
                });
        }

        return Results.Accepted();
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

    private static IResult GetHistory(string nodeId, TelemetryStore store)
    {
        List<ReadingDto> readings =
        [
            .. store.Floats.GetRecent(nodeId).Select(ToReading),
            .. store.Ints.GetRecent(nodeId).Select(ToReading),
            .. store.Bools.GetRecent(nodeId).Select(ToReading)
        ];

        if (readings.Count == 0)
        {
            return Results.NotFound();
        }

        readings.Sort((a, b) => a.TimestampUtc.CompareTo(b.TimestampUtc));

        return Results.Ok(new NodeHistoryDto
        {
            NodeId = nodeId,
            Count = readings.Count,
            Readings = readings
        });
    }

    private static TelemetryPacket<T> ToPacket<T>(TelemetryPacketDto dto, T value) where T : struct =>
        new(dto.NodeId, dto.TimestampUtc, dto.Category, value);

    private static ReadingDto ToReading<T>(TelemetryPacket<T> packet) where T : struct =>
        new() { TimestampUtc = packet.TimestampUtc, Value = packet.Value };

    // Minimal APIs do not validate automatically
    private static Dictionary<string, string[]> Validate(TelemetryPacketDto dto)
    {
        Dictionary<string, string[]> errors = [];

        if (string.IsNullOrWhiteSpace(dto.NodeId))
        {
            errors[nameof(dto.NodeId)] = ["A node id is required."];
        }

        if (dto.Category == SensorCategory.Unspecified)
        {
            errors[nameof(dto.Category)] = ["A sensor category is required."];
        }

        if (dto.ValueKind == TelemetryValueKind.Unspecified)
        {
            errors[nameof(dto.ValueKind)] = ["A value kind is required."];
            return errors;
        }

        if (!dto.HasConsistentValue())
        {
            errors[nameof(dto.Value)] = [$"The value cannot be read as {dto.ValueKind}."];
        }

        TelemetryValueKind expected = TelemetryPacketDto.ExpectedKindFor(dto.Category);

        if (expected != TelemetryValueKind.Unspecified && dto.ValueKind != expected)
        {
            errors[nameof(dto.ValueKind)] =
                [$"A {dto.Category} sensor publishes {expected}, not {dto.ValueKind}."];
        }

        return errors;
    }
}
