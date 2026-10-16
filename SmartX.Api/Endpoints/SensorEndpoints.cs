using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

public static class SensorEndpoints
{
    public static RouteGroupBuilder MapSensorEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/sensors").WithTags("Sensors");

        group.MapPost("/", Register);
        group.MapGet("/", GetAll);
        group.MapGet("/{nodeId}", GetOne);
        group.MapDelete("/{nodeId}", Delete);

        return group;
    }

    private static IResult Register(
        SensorRegistrationDto dto,
        SensorRegistry registry,
        NodeStateTracker tracker,
        TelemetryStore store,
        DeviceTable devices)
    {
        Dictionary<string, string[]> errors =
            Validate(dto, out MacAddress mac, out DeploymentPath location);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (registry.IsMacInUse(mac))
        {
            return Results.Problem($"MAC {mac} is already registered to another node.", statusCode: 409);
        }

        SensorRegistration registration = new()
        {
            NodeId = dto.NodeId.Trim(),
            Mac = mac,
            Location = location,
            Category = dto.Category,
            ExpectedInterval = TimeSpan.FromMilliseconds(dto.ExpectedIntervalMs)
        };

        if (!registry.TryAdd(registration))
        {
            return Results.Problem($"Node '{registration.NodeId}' is already registered.", statusCode: 409);
        }

        devices.Add(registration);

        return Results.Created($"/api/sensors/{registration.NodeId}", ToDto(registration, tracker, store));
    }

    private static IResult GetAll(SensorRegistry registry, NodeStateTracker tracker, TelemetryStore store) =>
        Results.Ok(registry.All().Select(sensor => ToDto(sensor, tracker, store)));

    private static IResult GetOne(
        string nodeId,
        SensorRegistry registry,
        NodeStateTracker tracker,
        TelemetryStore store) =>
        registry.TryGet(nodeId, out SensorRegistration? registration)
            ? Results.Ok(ToDto(registration, tracker, store))
            : Results.NotFound();

    private static IResult Delete(string nodeId, SensorRegistry registry, DeviceTable devices)
    {
        if (!registry.Remove(nodeId))
        {
            return Results.NotFound();
        }

        devices.Remove(nodeId);
        return Results.NoContent();
    }

    private static SensorDto ToDto(
        SensorRegistration registration,
        NodeStateTracker tracker,
        TelemetryStore store) => new()
    {
        NodeId = registration.NodeId,
        Mac = registration.Mac.ToString(),
        Location = registration.Location.ToString(),
        Category = registration.Category,
        ExpectedIntervalMs = (int)registration.ExpectedInterval.TotalMilliseconds,
        RegisteredAtUtc = registration.RegisteredAtUtc,
        Liveness = tracker.Get(registration.NodeId),
        LastSeenUtc = store.LastSeenUtc(registration)
    };

    // Out parameters so the parsed values can be reused instead of parsing twice
    private static Dictionary<string, string[]> Validate(
        SensorRegistrationDto dto,
        out MacAddress mac,
        out DeploymentPath location)
    {
        Dictionary<string, string[]> errors = [];
        mac = default;
        location = default;

        if (string.IsNullOrWhiteSpace(dto.NodeId))
        {
            errors[nameof(dto.NodeId)] = ["A node id is required."];
        }

        if (!MacAddress.TryParse(dto.Mac, out mac))
        {
            errors[nameof(dto.Mac)] = ["Expected a MAC address like AA:BB:CC:DD:EE:FF."];
        }

        if (!DeploymentPath.TryParse(dto.Location, out location))
        {
            errors[nameof(dto.Location)] =
                [$"Expected a path like FacilityA/Zone1/SubZoneB, at most {DeploymentPath.MaxDepth} levels deep."];
        }

        if (dto.Category == SensorCategory.Unspecified)
        {
            errors[nameof(dto.Category)] = ["A sensor category is required."];
        }

        TimeSpan interval = TimeSpan.FromMilliseconds(dto.ExpectedIntervalMs);

        if (interval < SensorRegistration.MinExpectedInterval ||
            interval > SensorRegistration.MaxExpectedInterval)
        {
            errors[nameof(dto.ExpectedIntervalMs)] =
            [
                $"Must be between {SensorRegistration.MinExpectedInterval.TotalMilliseconds} " +
                $"and {SensorRegistration.MaxExpectedInterval.TotalMilliseconds} ms."
            ];
        }

        return errors;
    }
}
