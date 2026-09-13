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

        return group;
    }

    private static IResult Ingest(TelemetryPacketDto dto, ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger("Telemetry");

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
                Accept(dto, floatValue, logger);
                break;

            case TelemetryValueKind.Int:
                dto.TryReadInt(out int intValue);
                Accept(dto, intValue, logger);
                break;

            case TelemetryValueKind.Bool:
                dto.TryReadBool(out bool boolValue);
                Accept(dto, boolValue, logger);
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

    // Storage arrives in a later step, for now accepting a packet just logs it
    private static void Accept<T>(TelemetryPacketDto dto, T value, ILogger logger) where T : struct
    {
        TelemetryPacket<T> packet = new(dto.NodeId, dto.TimestampUtc, dto.Category, value);
        logger.LogInformation("Accepted {Packet}", packet.ToString());
    }

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
